using System;
using System.Collections.Generic;
using System.Linq;
using One.Inception.Workflow;

namespace One.Inception.MessageProcessing;

public class HandlerSubscriber : SubscriberBase
{
    public HandlerSubscriber(Type handlerType, Workflow<HandleContext> handlerWorkflow) : base(handlerType, handlerWorkflow) { }

    public override IEnumerable<Type> GetInvolvedMessageTypes()
    {
        Type baseMessageType = typeof(IMessage);

        return handlerType.GetInterfaces()
            .Where(x => x.IsGenericType && x.GetGenericArguments().Length == 1 && (baseMessageType.IsAssignableFrom(x.GetGenericArguments()[0])))
            .Select(@interface => @interface.GetGenericArguments()[0])
            .Distinct();
    }
}
