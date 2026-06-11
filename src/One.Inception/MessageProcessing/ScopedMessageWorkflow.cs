using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace One.Inception.MessageProcessing;

public class InceptionMessageTracer : ITracer
{
    private readonly IInceptionContextAccessor contextAccessor;

    public InceptionMessageTracer(IInceptionContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor;
    }

    public TraceInfo GenerateTrace(string messageId = null)
    {
        if (string.IsNullOrEmpty(messageId))
            messageId = Guid.NewGuid().ToString();

        if (contextAccessor.Context is null)
            return new TraceInfo(messageId, messageId, messageId);

        string causationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CausationId, out object causationIdObj) ? causationIdObj.ToString() : messageId;
        string correlationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CorrelationId, out object correlationIdObj) ? correlationIdObj.ToString() : messageId;

        TraceInfo traceInfo = new TraceInfo(messageId, causationId, correlationId);

        return traceInfo;
    }

    public void Record(string incomingMessageId, string correlationId = null)
    {
        if (string.IsNullOrEmpty(incomingMessageId) == false)
        {
            contextAccessor.Context.Trace[MessageHeader.CausationId] = incomingMessageId;

            if (contextAccessor.Context.Trace.ContainsKey(MessageHeader.CorrelationId) == false)
            {
                if (string.IsNullOrEmpty(correlationId))
                {
                    contextAccessor.Context.Trace[MessageHeader.CorrelationId] = incomingMessageId;
                }
                else
                {
                    contextAccessor.Context.Trace[MessageHeader.CorrelationId] = correlationId;
                }
            }
        }
    }

    public TraceInfo GetTrace()
    {
        string theMsgId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.MessageId, out object messageId) ? messageId.ToString() : null;
        string theCausId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CausationId, out object causationId) ? causationId.ToString() : null;
        string theCorrId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CorrelationId, out object correlationId) ? correlationId.ToString() : null;

        TraceInfo traceInfo = new TraceInfo(theMsgId, theCausId, theCorrId);

        return traceInfo;
    }
}

public class ScopedMessageWorkflow : Workflow<HandleContext>
{
    private const string ErrorMessage = "Somehow the IServiceScope has been already created and there will be an unexpected behavior after this message.";

    private static ConcurrentDictionary<HandleContext, IServiceScope> scopes = new ConcurrentDictionary<HandleContext, IServiceScope>();
    public static IServiceScope GetScope(HandleContext context) => scopes[context];

    private readonly DefaultContextFactory contextFactory;
    readonly Workflow<HandleContext> workflow;
    private readonly IServiceScopeFactory serviceScopeFactory;

    public ScopedMessageWorkflow(Workflow<HandleContext> workflow, IServiceProvider serviceProvider)
    {
        this.workflow = workflow;
        this.contextFactory = serviceProvider.GetRequiredService<DefaultContextFactory>();
        this.serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    protected override Execution<HandleContext> CreateExecutionContext(HandleContext handleContext)
    {
        object tenant = handleContext.Message.GetTenant();

        bool hasScopeError = false;

        IServiceScope scope = default;
        if (scopes.TryGetValue(handleContext, out scope) == false)
        {
            scope = serviceScopeFactory.CreateScope();
            if (scopes.TryAdd(handleContext, scope) == false)
                hasScopeError = true;

            InceptionContext context = contextFactory.Create(tenant ?? handleContext.Message, scope.ServiceProvider);
            foreach (var header in handleContext.Message.Headers)
            {
                context.Trace.Add(header.Key, header.Value);
            }
        }

        handleContext.ServiceProvider = scope.ServiceProvider;

        ILogger<ScopedMessageWorkflow> logger = handleContext.ServiceProvider.GetRequiredService<ILogger<ScopedMessageWorkflow>>();
        handleContext.LoggerScope = logger.BeginScope(scope => scope.AddScope(Log.Tenant, tenant));

        if (hasScopeError)
        {
            logger.LogCritical(ErrorMessage);
            throw new Exception(ErrorMessage);
        }

        try
        {
            // tracing begins here
            ITracer tracer = handleContext.ServiceProvider.GetRequiredService<InceptionMessageTracer>();
            tracer.Record(handleContext.Message.Id.ToString());
            // tracing ends here
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Failed to record trace for {inception_MessageType}. Message is still handled, do not worry... but tracing is lost. {@inception_Message}", handleContext.Message.GetMessageType().Name, handleContext.Message))) { }

        return base.CreateExecutionContext(handleContext);
    }

    protected override Task OnRunCompletedAsync(Execution<HandleContext> execution)
    {
        if (scopes.TryRemove(execution.Context, out IServiceScope serviceScope))
        {
            execution.Context.ServiceProvider = null;
            serviceScope.Dispose();

            if (execution.Context.ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        execution.Context.LoggerScope?.Dispose();

        return Task.CompletedTask;
    }

    protected override Task RunAsync(Execution<HandleContext> execution)
    {
        return workflow.RunAsync(execution.Context);
    }
}
