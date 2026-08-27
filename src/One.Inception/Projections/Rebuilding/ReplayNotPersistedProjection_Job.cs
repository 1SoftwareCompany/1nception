using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using One.Inception.Cluster.Job;
using One.Inception.EventStore;
using One.Inception.MessageProcessing;
using One.Inception.Projections.Cassandra.EventSourcing;
using One.Inception.Projections.Versioning;
using One.Inception.Serializer;
using One.Inception.Workflow;

namespace One.Inception.Projections.Rebuilding;

public sealed class ReplayNotPersistedProjection_Job : InceptionJob<ReplayNotPersistedProjection_JobData>
{
    private readonly IPublisher<ISystemSignal> signalPublisher;
    private readonly ISerializer serializer;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly IEventStorePlayer player;
    private readonly ProgressTracker progressTracker;
    private readonly ProjectionVersionHelper projectionVersionHelper;
    private readonly IEventLookUp eventLookUp;
    private static readonly Action<ILogger, string, ulong, double, Exception> LogProjectionProgress =
        LoggerMessage.Define<string, ulong, double>(LogLevel.Information, InceptionLogEvent.JobOk, "Rebuild projection job progress for version {inception_projection_version}: {counter}. Average speed: {speed} events/s.");

    private static readonly Action<ILogger, string, Exception> LogRebuildProjectionCanceled =
        LoggerMessage.Define<string>(LogLevel.Information, InceptionLogEvent.JobError, "The rebuild job for version {inception_projection_version} was cancelled.");

    private static readonly Action<ILogger, string, double, ulong, double, Exception> LogRebuildProjectionCompleted =
        LoggerMessage.Define<string, double, ulong, double>(LogLevel.Information, InceptionLogEvent.JobOk, "The rebuild job for version {inception_projection_version} has completed in {ElapsedMilliseconds:0.0000}ms. Total events: {counter}. Average speed: {speed} events/s.");

    public ReplayNotPersistedProjection_Job(
        IInitializableProjectionStore projectionStoreInitializer,
        IEventStorePlayer player,
        IProjectionWriter projectionWriter,
        ProgressTracker progressTracker,
        ProjectionVersionHelper projectionVersionHelper,
        IPublisher<ISystemSignal> signalPublisher,
        ISerializer serializer,
        IInceptionContextAccessor contextAccessor,
        ILogger<ReplayNotPersistedProjection_Job> logger,
        IEventLookUp eventLookUp)
        : base(logger)
    {
        this.signalPublisher = signalPublisher;
        this.serializer = serializer;
        this.contextAccessor = contextAccessor;
        this.progressTracker = progressTracker;
        this.projectionVersionHelper = projectionVersionHelper;
        this.player = player;
        this.eventLookUp = eventLookUp;
    }

    public override string Name { get; set; } = nameof(ReplayNotPersistedProjection_Job);

    protected override async Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        if (Data.IsCompleted)
            return JobExecutionStatus.Completed;

        ProjectionVersion version = Data.Version;
        Type projectionType = version.ProjectionName.GetTypeByContract();

        var startSignal = progressTracker.GetProgressStartedSignal();
        await signalPublisher.PublishAsync(startSignal).ConfigureAwait(false);

        var pingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        bool isPersistedProjection = projectionType.IsPersistedProjection();
        if (isPersistedProjection)
        {
            logger.LogWarning("The projection does is not marked with attribute {attribute} with setting {setting}. Canceling...", nameof(ProjectionAttribute), nameof(ProjectionReplaySetting.Ordered));
            if (Data.IsCanceled == false)
                await CancelJobAsync(cluster).ConfigureAwait(false);

            pingSource.Cancel();
            LogRebuildProjectionCanceled(logger, version.ToString(), null);
            return JobExecutionStatus.Canceled;
        }

        var projectionInstance = contextAccessor.Context.ServiceProvider.GetRequiredService(projectionType) as IProjection;

        bool isOrderedReplay = projectionType.IsProjectionReplayOrdered();

        progressTracker.MarkProcessStart();
        TimeSpan elapsed = default;

        List<string> projectionHandledEventTypes = projectionVersionHelper.GetInvolvedEventTypes(projectionType).Select(x => x.GetContractId()).ToList();
        if (isOrderedReplay == false)
        {
            foreach (string eventTypeId in projectionHandledEventTypes)
            {
                if (version.ProjectionName.Equals(ProjectionVersionsHandler.ContractId) == false)
                {
                    if (Data.IsCanceled)
                    {
                        LogRebuildProjectionCanceled(logger, version.ToString(), null);
                        return JobExecutionStatus.Canceled;
                    }

                    if (cancellationToken.IsCancellationRequested || await projectionVersionHelper.ShouldBeCanceledAsync(version, Data.DueDate).ConfigureAwait(false))
                    {
                        await CancelJobAsync(cluster).ConfigureAwait(false);

                        LogRebuildProjectionCanceled(logger, version.ToString(), null);
                        return JobExecutionStatus.Canceled;
                    }
                }

                PlayerOperator playerOperator = new PlayerOperator()
                {
                    OnLoadAsync = async eventRaw =>
                    {
                        IEvent @event = serializer.DeserializeFromBytes<IEvent>(eventRaw.Data);
                        @event = @event.Unwrap();
                        if (@event is null)
                        {
                            logger.LogError("Failed to deserialize event from data {data}.", eventRaw.Data);
                            return;
                        }

                        await projectionInstance.ReplayEventAsync(@event).ConfigureAwait(false);

                        progressTracker.TrackAndNotify(@event.GetType().GetContractId(), pingSource.Token);
                    },
                    NotifyProgressAsync = async options =>
                    {
                        var totalCount = progressTracker.GetTotalProcessedCount();
                        Data.ProcessedCount = totalCount;
                        Data.PaginationToken = options.PaginationToken;
                        Data.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
                        Data = await cluster.PingAsync(Data).ConfigureAwait(false);

                        var avgSpeed = progressTracker.GetProcessedPerSecond();
                        LogProjectionProgress(logger, version.ToString(), totalCount, avgSpeed, null);
                    }
                };

                if (Data.IsCanceled || cancellationToken.IsCancellationRequested || await projectionVersionHelper.ShouldBeCanceledAsync(version, Data.DueDate).ConfigureAwait(false))
                {
                    if (Data.IsCanceled == false)
                        await CancelJobAsync(cluster).ConfigureAwait(false);

                    LogRebuildProjectionCanceled(logger, version.ToString(), null);
                    return JobExecutionStatus.Canceled;
                }

                PlayerOptions opt = new PlayerOptions()
                {
                    EventTypeId = eventTypeId,
                    PaginationToken = Data.PaginationToken,
                    After = Data.After,
                    Before = Data.Before ?? DateTimeOffset.UtcNow,
                    MaxDegreeOfParallelism = Data.MaxDegreeOfParallelism
                };

                await player.EnumerateEventStore(playerOperator, opt, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            PlayerOperator playerOperator = new PlayerOperator()
            {
                OnAggregateStreamLoadedAsync = async stream =>
                {
                    // 1. It is important to check all EventHandlers if they are interested in specific events and do this FIRST.
                    List<AggregateEventRaw> rawEvents = [];
                    foreach (string eventTypeContract in projectionHandledEventTypes)
                    {
                        IEnumerable<AggregateEventRaw> interested = stream.Commits
                            .SelectMany(x => x.Events)
                            .Where(x => IsInterested(eventTypeContract, x.Data));

                        // Do not try to optimize the GC using Enumerable.Concat because it does not solve the issue
                        rawEvents.AddRange(interested);
                    }

                    // 2. Then the result is sorted!
                    IEnumerable<AggregateEventRaw> rawEventsSorted = rawEvents.OrderBy(x => x.Revision).ThenBy(x => x.Position); // Do not try to optimize the sorting with SortedDictionary. It is not faster.
                    foreach (AggregateEventRaw eventRaw in rawEventsSorted)
                    {
                        var @event = serializer.DeserializeFromBytes<IEvent>(eventRaw.Data).Unwrap();
                        if (@event is null)
                        {
                            logger.LogError("Failed to deserialize event from data {data}.", eventRaw.Data);
                            return;
                        }

                        try
                        {
                            await projectionInstance.ReplayEventAsync(@event);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to replay event!");

                            continue;
                        }

                        progressTracker.TrackAndNotify(@event.GetType().GetContractId(), cancellationToken);
                    }
                },
                NotifyProgressAsync = async options =>
                {
                    var totalCount = progressTracker.GetTotalProcessedCount();
                    Data.ProcessedCount = totalCount;
                    Data.PaginationToken = options.PaginationToken;
                    Data.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
                    Data = await cluster.PingAsync(Data).ConfigureAwait(false);

                    var avgSpeed = progressTracker.GetProcessedPerSecond();
                    LogProjectionProgress(logger, version.ToString(), totalCount, avgSpeed, null);
                }
            };

            if (Data.IsCanceled || cancellationToken.IsCancellationRequested || await projectionVersionHelper.ShouldBeCanceledAsync(version, Data.DueDate).ConfigureAwait(false))
            {
                if (Data.IsCanceled == false)
                    await CancelJobAsync(cluster).ConfigureAwait(false);

                LogRebuildProjectionCanceled(logger, version.ToString(), null);
                return JobExecutionStatus.Canceled;
            }

            PlayerOptions opt = new PlayerOptions()
            {
                PaginationToken = Data.PaginationToken,
                After = Data.After,
                Before = Data.Before ?? DateTimeOffset.UtcNow,
                MaxDegreeOfParallelism = Data.MaxDegreeOfParallelism
            };

            await player.EnumerateEventStore(playerOperator, opt, cancellationToken).ConfigureAwait(false);

            await projectionInstance.OnReplayCompletedAsync().ConfigureAwait(false);
            elapsed = progressTracker.GetElapsed();
        }

        pingSource.Cancel();
        Data.IsCompleted = true;
        Data.Timestamp = DateTimeOffset.UtcNow;
        Data = await cluster.PingAsync(Data).ConfigureAwait(false);

        var finishSignal = progressTracker.GetProgressFinishedSignal();
        await signalPublisher.PublishAsync(finishSignal).ConfigureAwait(false);

        var totalCount = progressTracker.GetTotalProcessedCount();
        var avgSpeed = progressTracker.GetProcessedPerSecond();

        LogRebuildProjectionCompleted(logger, version.ToString(), elapsed.TotalMilliseconds, totalCount, avgSpeed, null);

        return JobExecutionStatus.Completed;
    }

    private async Task CancelJobAsync(IClusterOperations cluster)
    {
        Data.IsCanceled = true;
        Data.Timestamp = DateTimeOffset.UtcNow;
        Data = await cluster.PingAsync(Data).ConfigureAwait(false);

        var finishSignal = progressTracker.GetProgressFinishedSignal();

        await signalPublisher.PublishAsync(finishSignal).ConfigureAwait(false);
    }

    private bool IsInterested(string eventTypeContract, byte[] data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(eventTypeContract);
        var eventSpan = bytes.AsSpan();
        return eventLookUp.HasEventId(data.AsSpan(), eventSpan);
    }
}

public class ReplayNotPersistedProjection_JobData : IJobData
{
    public ReplayNotPersistedProjection_JobData()
    {
        IsCompleted = false;
        IsCanceled = false;
        Timestamp = DateTimeOffset.UtcNow;
        DueDate = DateTimeOffset.MaxValue;
    }

    public bool IsCompleted { get; set; }
    public bool IsCanceled { get; set; }
    public string PaginationToken { get; set; }
    public ulong ProcessedCount { get; set; }
    public ulong TotalCount { get; set; }
    public ProjectionVersion Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public DateTimeOffset? After { get; set; }
    public DateTimeOffset? Before { get; set; }
    public int MaxDegreeOfParallelism { get; set; }
}
