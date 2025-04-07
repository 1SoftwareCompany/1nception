using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using One.Inception.MessageProcessing;
using Microsoft.Extensions.Logging;

namespace One.Inception.Workflow;

public static class LogOption
{
    public static LogDefineOptions SkipLogInfoChecks = new LogDefineOptions() { SkipEnabledCheck = true };
}

internal static class InceptionLogEvent
{
    public static EventId Host = new EventId(74000, "InceptionHost");
    public static EventId WorkflowHandle = new EventId(74001, "InceptionWorkflowHandle");
    public static EventId EventStoreRead = new EventId(74010, "InceptionEventStoreRead");
    public static EventId EventStoreWrite = new EventId(74011, "InceptionEventStoreWrite");
    public static EventId ProjectionRead = new EventId(74020, "InceptionProjectionRead");
    public static EventId ProjectionWrite = new EventId(74021, "InceptionProjectionWrite");
    public static EventId JobOk = new EventId(74100, "InceptionJobOk");
    public static EventId JobError = new EventId(74101, "InceptionJobError");
    public static EventId PublishOk = new EventId(74200, "InceptionPublishOk");
    public static EventId PublishError = new EventId(74201, "InceptionPublishError");
}

public sealed class DiagnosticsWorkflow<TContext> : Workflow<TContext> where TContext : HandleContext
{
    private static readonly ILogger logger = InceptionLogger.CreateLogger(typeof(DiagnosticsWorkflow<>));
    private static readonly Action<ILogger, string, string, double, Exception> LogHandleSuccess = LoggerMessage.Define<string, string, double>(LogLevel.Information, InceptionLogEvent.WorkflowHandle, "{inception_MessageHandler} handled {inception_MessageType} in {ElapsedMilliseconds:0.0000}ms.", LogOption.SkipLogInfoChecks);
    private static readonly Action<ILogger, string, string, Exception> LogHandleStarting = LoggerMessage.Define<string, string>(LogLevel.Debug, InceptionLogEvent.WorkflowHandle, "{inception_MessageHandler} starting handle {inception_MessageType}.", LogOption.SkipLogInfoChecks);

    private const string ActivityName = "One.Inception.Hosting.Workflow";
    private const string DiagnosticsUnhandledExceptionKey = "One.Inception.Hosting.UnhandledException";

    readonly Workflow<TContext> workflow;
    private readonly DiagnosticListener diagnosticListener;
    private readonly ActivitySource activitySource;

    public DiagnosticsWorkflow(Workflow<TContext> workflow, DiagnosticListener diagnosticListener, ActivitySource activitySource)
    {
        this.workflow = workflow;
        this.diagnosticListener = diagnosticListener;
        this.activitySource = activitySource;
    }

    protected override async Task RunAsync(Execution<TContext> execution)
    {
        if (execution is null) throw new ArgumentNullException(nameof(execution));

        using (logger.BeginScope(scope =>
        {
            string tenant = execution.Context.Message.GetTenant();
            scope.AddScope(Log.Tenant, tenant);

            if (execution.Context.Message.TryGetRootId(out string rootId))
                scope.AddScope(Log.AggregateId, rootId);
        }))
        {
            Activity activity = StartActivity(execution.Context);

            Type msgType = execution.Context.Message.Payload.GetType();

            LogHandleStarting(logger, execution.Context.HandlerType.Name, msgType.Name, null);
            if (logger.IsEnabled(LogLevel.Information))
            {
                long startTimestamp = 0;
                startTimestamp = Stopwatch.GetTimestamp();

                await workflow.RunAsync(execution.Context).ConfigureAwait(false);

                TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

                LogHandleSuccess(logger, execution.Context.HandlerType.Name, msgType.Name, elapsed.TotalMilliseconds, null);
            }
            else
            {
                await workflow.RunAsync(execution.Context).ConfigureAwait(false);
            }

            StopActivity(activity);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Activity StartActivity(TContext context)
    {
        if (diagnosticListener.IsEnabled())
        {
            Activity activity = null;
            string parentId = string.Empty;
            context.Message.Headers.TryGetValue("telemetry_traceparent", out parentId);
            string activityName = $"{context.HandlerType.Name}__{context.Message.Payload.GetType().Name}";
            if (ActivityContext.TryParse(parentId, null, out ActivityContext ctx))
            {
                activity = activitySource.CreateActivity(activityName, ActivityKind.Server, ctx);
            }
            else
            {
                activity = activitySource.CreateActivity(activityName, ActivityKind.Server, parentId);
            }

            if (activity is null)
            {
                activity = new Activity(activityName);

                if (string.IsNullOrEmpty(parentId) == false)
                    activity.SetParentId(parentId);
            }

            activity.SetTag(Log.MessageId, context.Message.Id.ToString());
            activity.Start();

            return activity;
        }

        return null;
    }

    private void StopActivity(Activity activity)
    {
        if (activity is null) return;
        // Stop sets the end time if it was unset, but we want it set before we issue the write
        // so we do it now.
        if (activity.Duration == TimeSpan.Zero)
        {
            activity.SetEndTime(DateTime.UtcNow);
        }
        diagnosticListener.Write(ActivityName, activity);
        activity.Stop();    // Resets Activity.Current (we want this after the Write)
    }
}
