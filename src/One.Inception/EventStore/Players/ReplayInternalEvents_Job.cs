using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.Cluster.Job;
using One.Inception.MessageProcessing;

namespace One.Inception.EventStore.Players;

public class ReplayInternalEvents_Job : InceptionJob<ReplayInternalEvents_JobData>
{
    private readonly IPublisher<IEvent> eventPublisher;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly IEventStorePlayer player;

    public ReplayInternalEvents_Job(IPublisher<IEvent> publicEventPublisher, IInceptionContextAccessor contextAccessor, IEventStorePlayer eventStorePlayer, ILogger<ReplayInternalEvents_Job> logger) : base(logger)
    {
        this.eventPublisher = publicEventPublisher;
        this.contextAccessor = contextAccessor;
        this.player = eventStorePlayer;
    }
    public override string Name { get; set; } = "35cfb5dd-f11c-45b5-a508-2b14a3266705";

    protected override async Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        if (Data.IsCompleted)
            return JobExecutionStatus.Completed;

        PlayerOptions opt = new PlayerOptions()
        {
            EventTypeId = Data.SourceEventTypeId,
            PaginationToken = Data.EventTypePaging?.PaginationToken,
            After = Data.After,
            Before = Data.Before ?? DateTimeOffset.UtcNow,
            AggregateRootId = Data.AggregateId is not null ? new Urn(Data.AggregateId) : null,
            ShouldReplayLastEventOnly = Data.ShouldReplayLastEventOnly
        };

        Type messageType = Data.SourceEventTypeId.GetTypeByContract();
        string boundedContext = messageType.GetBoundedContext();

        ulong counter = Data.EventTypePaging is null ? 0 : Data.EventTypePaging.ProcessedCount;
        PlayerOperator @operator = new PlayerOperator()
        {
            OnLoadAsync = async eventRaw =>
            {
                string tenant = contextAccessor.Context.Tenant;
                //TODO: Document which headers are essential or make another ctor for InceptionMessage with byte[]
                var headers = new Dictionary<string, string>()
                {
                    { MessageHeader.RecipientHandlers, Data.RecipientHandlers },
                    { MessageHeader.PublishTimestamp, DateTime.UtcNow.ToFileTimeUtc().ToString() },
                    { MessageHeader.Tenant, tenant },
                    { MessageHeader.BoundedContext, boundedContext },
                    { "contract_name", Data.SourceEventTypeId }
                };

                await eventPublisher.PublishAsync(eventRaw.Data, Data.SourceEventTypeId.GetTypeByContract(), tenant, headers).ConfigureAwait(false);

                counter++;
            },
            NotifyProgressAsync = async options =>
            {
                var progress = new EventPaging(options.EventTypeId, options.PaginationToken, options.After, options.Before, counter, 0);
                Data.EventTypePaging = progress;
                Data.Timestamp = DateTimeOffset.UtcNow;
                Data = await cluster.PingAsync(Data).ConfigureAwait(false);
            }
        };

        await player.EnumerateEventStore(@operator, opt, cancellationToken).ConfigureAwait(false);

        Data.IsCompleted = true;
        Data.Timestamp = DateTimeOffset.UtcNow;
        Data = await cluster.PingAsync(Data).ConfigureAwait(false);

        logger.LogInformation("A job has completed.");
        return JobExecutionStatus.Completed;
    }
}

public class ReplayInternalEvents_JobFactory
{
    private readonly ReplayInternalEvents_Job job;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly BoundedContext boundedContext;

    public ReplayInternalEvents_JobFactory(ReplayInternalEvents_Job job, IOptions<BoundedContext> boundedContext, IInceptionContextAccessor contextAccessor)
    {
        this.job = job;
        this.contextAccessor = contextAccessor;
        this.boundedContext = boundedContext.Value;
    }

    public ReplayInternalEvents_Job CreateJob(ReplayInternalEventsRequested signal)
    {
        StringBuilder sb = new StringBuilder($"urn:{boundedContext.Name}:{contextAccessor.Context.Tenant}:{job.Name}:{signal.RecipientBoundedContext}:{signal.RecipientHandlers}:{signal.SourceEventTypeId}:{signal.ReplayOptions.ShouldReplayLastEventOnly}");

        if (string.IsNullOrEmpty(signal.ReplayOptions?.AggregateRootId?.Value) == false)
            sb.Append($":{signal.ReplayOptions.AggregateRootId.Value}");

        job.Name = sb.ToString();

        job.BuildInitialData(() => new ReplayInternalEvents_JobData()
        {
            After = signal.ReplayOptions.After,
            Before = signal.ReplayOptions.Before,
            RecipientBoundedContext = signal.RecipientBoundedContext,
            RecipientHandlers = signal.RecipientHandlers,
            SourceEventTypeId = signal.SourceEventTypeId,
            AggregateId = signal.ReplayOptions?.AggregateRootId?.Value,
            ShouldReplayLastEventOnly = signal.ReplayOptions.ShouldReplayLastEventOnly
        });

        return job;
    }
}

public class ReplayInternalEvents_JobData : IJobData
{
    public ReplayInternalEvents_JobData()
    {
        IsCompleted = false;
        Timestamp = DateTimeOffset.UtcNow;
        DueDate = DateTimeOffset.MaxValue;
    }

    public bool IsCompleted { get; set; }

    public EventPaging EventTypePaging { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public DateTimeOffset DueDate { get; set; }

    public DateTimeOffset? After { get; set; }
    public DateTimeOffset? Before { get; set; }
    public string RecipientBoundedContext { get; set; }
    public string RecipientHandlers { get; set; }
    public string SourceEventTypeId { get; set; }

    // -> for specific aggregate
    public string AggregateId { get; set; }
    public bool ShouldReplayLastEventOnly { get; set; }
}
