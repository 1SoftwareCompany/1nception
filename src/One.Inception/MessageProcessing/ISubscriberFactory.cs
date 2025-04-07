using System;

namespace One.Inception.MessageProcessing;

public interface ISubscriberFactory<out T>
{
    ISubscriber Create(Type handlerType);
}
