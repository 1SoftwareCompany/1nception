using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace One.Inception.MessageProcessing;

public interface ITracer
{
    void Record(string incomingMessageId, string correlationId = null);
    TraceInfo GenerateTrace(string messageId = null);
    TraceInfo GetTrace();
}

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

        string causationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CausationId, out object causationIdObj) ? causationIdObj.ToString() : messageId;
        string correlationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CorrelationId, out object correlationIdObj) ? correlationIdObj.ToString() : messageId;

        TraceInfo traceInfo = new TraceInfo
        {
            MessageId = messageId,
            CausationId = causationId,
            CorrelationId = correlationId
        };

        return traceInfo;
    }

    public void Record(string incomingMessageId, string correlationId = null)
    {
        if (contextAccessor.Context.Trace.ContainsKey(MessageHeader.CausationId) == false)
            contextAccessor.Context.Trace.Add(MessageHeader.CausationId, incomingMessageId);

        if (contextAccessor.Context.Trace.ContainsKey(MessageHeader.CorrelationId) == false)
            contextAccessor.Context.Trace.Add(MessageHeader.CorrelationId, correlationId ?? incomingMessageId);
    }

    public TraceInfo GetTrace()
    {
        TraceInfo traceInfo = new TraceInfo
        {
            MessageId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.MessageId, out object messageId) ? messageId.ToString() : null,
            CausationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CausationId, out object causationId) ? causationId.ToString() : null,
            CorrelationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CorrelationId, out object correlationId) ? correlationId.ToString() : null
        };

        return traceInfo;
    }
}

public record class TraceInfo
{


    public string MessageId { get; set; }
    public string CausationId { get; set; }
    public string CorrelationId { get; set; }
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

        // tracing begins here
        ITracer tracer = handleContext.ServiceProvider.GetRequiredService<InceptionMessageTracer>();
        tracer.Record(handleContext.Message.Id.ToString());
        // tracing ends here

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
