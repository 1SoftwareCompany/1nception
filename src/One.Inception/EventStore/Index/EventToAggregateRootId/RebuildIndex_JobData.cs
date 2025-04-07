using System;
using One.Inception.Cluster.Job;

namespace One.Inception.EventStore.Index;

public class RebuildIndex_JobData : IJobData
{
    public bool IsCompleted { get; set; } = false;

    public string PaginationToken { get; set; }

    public uint ProcessedCount { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public int MaxDegreeOfParallelism { get; set; }
}
