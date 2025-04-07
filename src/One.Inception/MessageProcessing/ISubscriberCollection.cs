using System.Collections.Generic;

namespace One.Inception.MessageProcessing;

public interface ISubscriberCollection<T>
{
    IEnumerable<ISubscriber> Subscribers { get; }

    IEnumerable<ISubscriber> GetInterestedSubscribers(InceptionMessage message);
    void Subscribe(ISubscriber subscriber);
    void UnsubscribeAll();
}
