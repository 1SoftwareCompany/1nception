using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace One.Inception.MessageProcessing;

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

            var context = contextFactory.Create(tenant ?? handleContext.Message, scope.ServiceProvider);
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
