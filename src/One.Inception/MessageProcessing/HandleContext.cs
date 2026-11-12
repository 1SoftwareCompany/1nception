using System;
using System.Threading;

namespace One.Inception.MessageProcessing;

public class HandleContext : IWorkflowContextWithServiceProvider
{
    public HandleContext(InceptionMessage message, Type handlerType, CancellationToken cancellationToken = default)
    {
        Message = message;
        HandlerType = handlerType;
        CancellationToken = cancellationToken;
    }

    public InceptionMessage Message { get; private set; }

    public Type HandlerType { get; private set; }

    public CancellationToken CancellationToken { get; }

    public IServiceProvider ServiceProvider { get; set; }

    internal IDisposable LoggerScope { get; set; }
}
