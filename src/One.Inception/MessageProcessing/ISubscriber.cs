using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception.MessageProcessing;

public interface ISubscriber
{
    string Id { get; }

    /// <summary>
    /// Gets the message types which the subscriber can process.
    /// </summary>
    /// <returns></returns>
    IEnumerable<Type> GetInvolvedMessageTypes();
    Type HandlerType { get; }
    Task ProcessAsync(InceptionMessage message);
}
