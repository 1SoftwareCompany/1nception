using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            return new MessageTraceInfo(messageId, messageId, messageId);

        string causationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CausationId, out object causationIdObj) ? causationIdObj.ToString() : messageId;
        string correlationId = contextAccessor.Context.Trace.TryGetValue(MessageHeader.CorrelationId, out object correlationIdObj) ? correlationIdObj.ToString() : messageId;

        MessageTraceInfo traceInfo = new MessageTraceInfo(messageId, causationId, correlationId);

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

public sealed class MessageTracer
{
    private readonly IEnumerable<IMessageTracer> _tracers;
    private readonly IEnumerable<IMessageTraceWriter> messageTraceWriters;

    public MessageTracer(IEnumerable<IMessageTracer> tracers, IEnumerable<IMessageTraceWriter> messageTraceWriters)
    {
        _tracers = tracers;
        this.messageTraceWriters = messageTraceWriters;
    }

    public async ValueTask<MessageTraceInfo> CreateTraceAsync(string messageId = null)
    {
        MessageTraceInfo traceInfo = null;
        foreach (IMessageTracer tracer in _tracers)
        {
            traceInfo = tracer.CreateTrace(messageId);
            if (IsFirstMessage(traceInfo) == false)
                break;
        }

        List<Task> writersTasks = new List<Task>();
        foreach (IMessageTraceWriter writer in messageTraceWriters)
        {
            writersTasks.Add(writer.WriteAsync(traceInfo));
        }
        await Task.WhenAll(writersTasks).ConfigureAwait(false);

        return traceInfo;
    }

    public void Record(string incomingMessageId, string correlationId = null)
    {
        foreach (IMessageTracer tracer in _tracers)
        {
            tracer.Record(incomingMessageId, correlationId);
        }
    }

    private static bool IsFirstMessage(MessageTraceInfo messageTraceInfo)
    {
        return
            messageTraceInfo.MessageId.Equals(messageTraceInfo.CausationId, StringComparison.OrdinalIgnoreCase)
            && messageTraceInfo.MessageId.Equals(messageTraceInfo.CorrelationId, StringComparison.OrdinalIgnoreCase);
    }
}
