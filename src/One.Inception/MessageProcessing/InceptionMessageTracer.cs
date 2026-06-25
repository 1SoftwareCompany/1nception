using System;
using One.MessageTracing;

namespace One.Inception.MessageProcessing;

public sealed class InceptionMessageTracer : IMessageTracer
{
    private readonly IInceptionContextAccessor contextAccessor;

    public InceptionMessageTracer(IInceptionContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor;
    }

    public MessageTraceInfo CreateTrace(string messageId = null)
    {
        if (string.IsNullOrEmpty(messageId))
            messageId = Guid.NewGuid().ToString();

        if (contextAccessor.Context is null)
            return new MessageTraceInfo(messageId, messageId, messageId, string.Empty);

        string causationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CausationId, out object causationIdObj) ? causationIdObj.ToString() : messageId;
        string correlationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CorrelationId, out object correlationIdObj) ? correlationIdObj.ToString() : messageId;

        MessageTraceInfo traceInfo = new MessageTraceInfo(messageId, causationId, correlationId, contextAccessor.Context.Tenant);

        return traceInfo;
    }

    public void Record(string incomingMessageId, string correlationId = null)
    {
        if (string.IsNullOrEmpty(incomingMessageId) == false)
        {
            contextAccessor.Context.Trace[MessageHeader.CausationId] = incomingMessageId;

            if (contextAccessor.Context.Trace.ContainsKey(MessageHeader.CorrelationId) == false)
            {
                if (string.IsNullOrEmpty(correlationId))
                {
                    contextAccessor.Context.Trace[MessageHeader.CorrelationId] = incomingMessageId;
                }
                else
                {
                    contextAccessor.Context.Trace[MessageHeader.CorrelationId] = correlationId;
                }
            }
        }
    }
}
