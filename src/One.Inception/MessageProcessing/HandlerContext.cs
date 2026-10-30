using System.Threading;

namespace One.Inception.MessageProcessing;

public class HandlerContext
{
    public HandlerContext(IMessage message, object handlerInstance, InceptionMessage inceptionMessage, CancellationToken cancellationToken = default)
    {
        Message = message;
        HandlerInstance = handlerInstance;
        InceptionMessage = inceptionMessage;
        CancellationToken = cancellationToken;
    }
    public IMessage Message { get; private set; }

    public CancellationToken CancellationToken { get; }

    public object HandlerInstance { get; private set; }

    public InceptionMessage InceptionMessage { get; private set; }

    public override string ToString()
    {
        return $"{HandlerInstance.GetType().Name}({Message.GetType().Name})";
    }
}
