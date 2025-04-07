using One.Inception.Workflow;
using System.Threading.Tasks;

namespace One.Inception.MessageProcessing;

/// <summary>
/// Work-flow which gets an object from the passed context and calls a method 'Index' with parameter '<see cref="InceptionMessage"/>'
/// <see cref="HandlerContext"/> should have 'HandlerInstance' and 'InceptionMessage' already set
/// </summary>
public class DynamicMessageIndex : Workflow<HandlerContext>
{
    protected override Task RunAsync(Execution<HandlerContext> execution)
    {
        dynamic handler = execution.Context.HandlerInstance;
        return handler.IndexAsync((dynamic)execution.Context.InceptionMessage);
    }
}
