using System.Threading.Tasks;
using One.Inception.Workflow;
using Microsoft.Extensions.Logging;

namespace One.Inception.MessageProcessing;

public sealed class LogExceptionOnHandleError : Workflow<ErrorContext>
{
    private static readonly ILogger logger = InceptionLogger.CreateLogger(typeof(LogExceptionOnHandleError));

    protected override Task RunAsync(Execution<ErrorContext> execution)
    {
        logger.LogError(execution.Context.Error, "There was an error in {inception_MessageHandler} while handling message {@inception_Message}", execution.Context.HandlerType.Name, execution.Context.Message);

        return Task.CompletedTask;
    }
}
