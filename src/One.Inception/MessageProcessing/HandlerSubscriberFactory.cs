using System;
using One.Inception.Workflow;

namespace One.Inception.MessageProcessing;

public class HandlerSubscriberFactory<T> : ISubscriberFactory<T>
{
    private readonly Workflow<HandleContext> workflow;

    public HandlerSubscriberFactory(ISubscriberWorkflowFactory<T> subscriberWorkflow)
    {
        workflow = subscriberWorkflow.GetWorkflow() as Workflow<HandleContext>;
    }

    public ISubscriber Create(Type handlerType)
    {
        return new HandlerSubscriber(handlerType, workflow);
    }
}
