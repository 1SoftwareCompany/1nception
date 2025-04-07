using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace One.Inception.MessageProcessing;

public class CreateScopedHandlerWorkflow : Workflow<HandleContext, IHandlerInstance>
{
    protected override Task<IHandlerInstance> RunAsync(Execution<HandleContext, IHandlerInstance> execution)
    {
        IServiceScope scope = ScopedMessageWorkflow.GetScope(execution.Context);
        IHandlerInstance handler = new DefaultHandlerInstance(scope.ServiceProvider.GetRequiredService(execution.Context.HandlerType));
        return Task.FromResult(handler);
    }
}
