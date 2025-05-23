﻿using One.Inception.Cluster.Job;
using One.Inception.EventStore.Index.Handlers;
using One.Inception.MessageProcessing;
using One.Inception.Projections;
using One.Inception.Projections.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.EventStore.Index;

public sealed class RebuildIndex_MessageCounter_Job : InceptionJob<RebuildEventCounterIndex_JobData>
{
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly TypeContainer<IEvent> eventTypes;
    private readonly IMessageCounter messageCounter;
    private readonly IProjectionReader projectionReader;
    private readonly IIndexStore indexStore;

    public RebuildIndex_MessageCounter_Job(IInceptionContextAccessor contextAccessor, TypeContainer<IEvent> eventTypes, IMessageCounter eventCounter, IProjectionReader projectionReader, IIndexStore indexStore, ILogger<RebuildIndex_MessageCounter_Job> logger) : base(logger)
    {
        this.contextAccessor = contextAccessor;
        this.eventTypes = eventTypes;
        this.messageCounter = eventCounter;
        this.projectionReader = projectionReader;
        this.indexStore = indexStore;
    }

    public override string Name { get; set; } = typeof(MessageCounterIndex).GetContractId();

    protected override async Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        // mynkow. this one fails
        IndexStatus indexStatus = await GetIndexStatusAsync<EventToAggregateRootId>().ConfigureAwait(false);
        if (indexStatus.IsNotPresent()) return JobExecutionStatus.Running;

        var pingSource = new CancellationTokenSource();
        CancellationToken ct = pingSource.Token;
        _ = Task.Run(async () =>
        {
            while (ct.IsCancellationRequested == false)
            {
                try
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Message counter cluster ping.");
                    await cluster.PingAsync(Data, ct).ConfigureAwait(false);
                    await Task.Delay(5000, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    logger.LogInformation("Message counter cluster ping has been stopped.");
                }
            }
        }, ct);

        foreach (Type eventType in eventTypes.Items)
        {
            string eventTypeId = eventType.GetContractId();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Message counter for {inception_MessageType} has been reset.", eventType.Name);

            var countTask = indexStore.GetCountAsync(eventTypeId);
            var resetTask = messageCounter.ResetAsync(eventType);

            long count = await countTask;
            await resetTask;
            await messageCounter.IncrementAsync(eventType, count).ConfigureAwait(false);
        }

        pingSource.Cancel();
        Data.IsCompleted = true;
        Data = await cluster.PingAsync(Data).ConfigureAwait(false);

        logger.LogInformation("The job has been completed.");

        return JobExecutionStatus.Completed;
    }


    async Task<IndexStatus> GetIndexStatusAsync<TIndex>() where TIndex : IEventStoreIndex
    {
        var id = new EventStoreIndexManagerId(typeof(TIndex).GetContractId(), contextAccessor.Context.Tenant);
        var result = await projectionReader.GetAsync<EventStoreIndexStatus>(id).ConfigureAwait(false);
        if (result.IsSuccess)
            return result.Data.State.Status;

        return IndexStatus.NotPresent;
    }
}

public class RebuildIndex_MessageCounter_JobFactory
{
    private readonly RebuildIndex_MessageCounter_Job job;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly BoundedContext boundedContext;

    public RebuildIndex_MessageCounter_JobFactory(RebuildIndex_MessageCounter_Job job, IOptions<BoundedContext> boundedContext, IInceptionContextAccessor contextAccessor)
    {
        this.job = job;
        this.contextAccessor = contextAccessor;
        this.boundedContext = boundedContext.Value;
    }

    public RebuildIndex_MessageCounter_Job CreateJob(VersionRequestTimebox timebox)
    {
        job.Name = $"urn:{boundedContext.Name}:{contextAccessor.Context.Tenant}:{job.Name}";
        job.BuildInitialData(() => new RebuildEventCounterIndex_JobData()
        {
            Timestamp = timebox.RequestStartAt
        });

        return job;
    }
}

public class RebuildEventCounterIndex_JobData : IJobData
{
    public RebuildEventCounterIndex_JobData()
    {
        IsCompleted = false;
        EventTypePaging = new List<EventTypeRebuildPaging>();
        Timestamp = DateTimeOffset.UtcNow;
        DueDate = DateTimeOffset.MaxValue;
    }

    public bool IsCompleted { get; set; }

    public List<EventTypeRebuildPaging> EventTypePaging { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public DateTimeOffset DueDate { get; set; }

    public class EventTypeRebuildPaging
    {
        public string Type { get; set; }

        public string PaginationToken { get; set; }
    }

    public void MarkPaginationTokenAsProcessed(string eventTypeId, string paginationToken)
    {
        EventTypeRebuildPaging existing = EventTypePaging.Where(et => et.Type.Equals(eventTypeId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        if (existing is null)
        {
            existing = new EventTypeRebuildPaging()
            {
                Type = eventTypeId,
                PaginationToken = paginationToken
            };

            EventTypePaging.Add(existing);
        }
        else
        {
            existing.PaginationToken = paginationToken;
        }
    }
}
