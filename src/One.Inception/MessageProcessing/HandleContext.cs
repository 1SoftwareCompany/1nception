using System;

namespace One.Inception.MessageProcessing;

public class HandleContext : IWorkflowContextWithServiceProvider
{
    public HandleContext(InceptionMessage message, Type handlerType)
    {
        Message = message;
        HandlerType = handlerType;
    }

    public InceptionMessage Message { get; private set; }

    public Type HandlerType { get; private set; }

    public IServiceProvider ServiceProvider { get; set; }

    internal IDisposable LoggerScope { get; set; }
}
