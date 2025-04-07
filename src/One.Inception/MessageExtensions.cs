using System;
using System.Text;
using One.Inception.EventStore.Index;
using One.Inception.Projections;

namespace One.Inception;

public static class MessageExtensions
{
    public static EventOrigin GetEventOrigin(this InceptionMessage message)
    {
        return new EventOrigin(Encoding.UTF8.GetBytes(GetRootId(message)), GetRevision(message), GetRootEventPosition(message), GetTimestamp(message));
    }

    public static EventOrigin GetEventOrigin(this IndexRecord indexRecord)
    {
        return new EventOrigin(indexRecord.AggregateRootId, indexRecord.Revision, indexRecord.Position, indexRecord.TimeStamp);
    }

    public static int GetRevision(this InceptionMessage message)
    {
        var revision = 0;
        var value = string.Empty;
        if (message.Headers.TryGetValue(MessageHeader.AggregateRootRevision, out value) && int.TryParse(value, out revision))
            return revision;
        return 0;
    }

    public static int GetRootEventPosition(this InceptionMessage message)
    {
        var revision = 0;
        var value = string.Empty;
        if (message.Headers.TryGetValue(MessageHeader.AggregateRootEventPosition, out value) && int.TryParse(value, out revision))
            return revision;
        return 0;
    }

    public static long GetPublishTimestamp(this InceptionMessage message)
    {
        long timestamp = 0;
        var value = string.Empty;
        if (message.Headers.TryGetValue(MessageHeader.PublishTimestamp, out value) && long.TryParse(value, out timestamp))
            return timestamp;
        return 0;
    }

    public static long GetTimestamp(this InceptionMessage message)
    {
        long timestamp = 0;
        var value = string.Empty;
        if (message.Headers.TryGetValue(MessageHeader.AggregateCommitTimestamp, out value) && long.TryParse(value, out timestamp))
            return timestamp;
        return 0;
    }

    public static string GetTenant(this InceptionMessage message)
    {
        string tenant = null;
        message.Headers.TryGetValue(MessageHeader.Tenant, out tenant);
        return tenant;
    }

    public static string GetRootId(this InceptionMessage message)
    {
        var aggregateRootId = string.Empty;
        if (message.Headers.TryGetValue(MessageHeader.AggregateRootId, out aggregateRootId))
            return aggregateRootId;

        throw new ArgumentException("Inception message does not contain a valid AggregateRootId");
    }

    public static bool TryGetRootId(this InceptionMessage message, out string aggregateRootId)
    {
        if (message.Headers.TryGetValue(MessageHeader.AggregateRootId, out aggregateRootId))
            return true;

        return false;
    }
}
