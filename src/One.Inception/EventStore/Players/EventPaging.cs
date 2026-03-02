using System;

namespace One.Inception.EventStore.Players;

public class EventPaging
{
    public EventPaging(string eventTypeId, string paginationToken, DateTimeOffset? after, DateTimeOffset? before, ulong processedCount, ulong totalCount)
    {
        Type = eventTypeId;
        PaginationToken = paginationToken;
        After = after;
        Before = before;
        ProcessedCount = processedCount;
        TotalCount = totalCount;
    }

    public string Type { get; set; }

    public string PaginationToken { get; set; }

    public DateTimeOffset? After { get; set; }
    public DateTimeOffset? Before { get; set; }

    public ulong ProcessedCount { get; set; }

    public ulong TotalCount { get; set; }
}
