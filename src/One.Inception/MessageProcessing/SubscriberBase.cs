using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using One.Inception.Workflow;

namespace One.Inception.MessageProcessing;

public abstract class SubscriberBase : ISubscriber
{
    protected readonly Type handlerType;
    protected readonly Workflow<HandleContext> handlerWorkflow;

    public SubscriberBase(Type handlerType, Workflow<HandleContext> handlerWorkflow)
    {
        this.handlerType = handlerType;
        this.handlerWorkflow = handlerWorkflow;
        bool hasDataContract = handlerType.HasAttribute<DataContractAttribute>();
        Id = hasDataContract ? handlerType.GetContractId() : handlerType.FullName;
    }

    public string Id { get; protected set; }

    public Type HandlerType => handlerType;

    public Task ProcessAsync(InceptionMessage message)
    {
        var context = new HandleContext(message, handlerType);
        return handlerWorkflow.RunAsync(context);
    }

    /// <summary>
    /// Gets all message types which this subscriber is interested in.
    /// </summary>
    /// <returns>Returns the message types.</returns>
    public abstract IEnumerable<Type> GetInvolvedMessageTypes();
}
