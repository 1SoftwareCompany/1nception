using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using One.Inception.Cluster.Job;

namespace One.Inception.EventStore.Index.Handlers;

[DataContract(Name = "055f2407-6b5a-4f77-92b0-fcae4c8d86a7")]
public class EventStoreIndexBuilder : ProcessManager, ISystemProcessManager,
    IEventHandle<EventStoreIndexRequested>,
    IProcessManagerTimeoutHandle<RebuildIndexInternal>,
    IProcessManagerTimeoutHandle<EventStoreIndexRebuildTimedout>
{
    private readonly IInceptionJobRunner jobRunner;
    private readonly IRebuildIndex_EventToAggregateRootId_JobFactory jobFactory;
    private readonly RebuildIndex_MessageCounter_JobFactory messageCounterJobFactory;

    public EventStoreIndexBuilder(IPublisher<ICommand> commandPublisher, IPublisher<IScheduledMessage> timeoutRequestPublisher, IInceptionJobRunner jobRunner, IRebuildIndex_EventToAggregateRootId_JobFactory jobFactory, RebuildIndex_MessageCounter_JobFactory messageCounterJobFactory)
        : base(commandPublisher, timeoutRequestPublisher)
    {
        this.jobRunner = jobRunner;
        this.jobFactory = jobFactory;
        this.messageCounterJobFactory = messageCounterJobFactory;
    }

    public async Task HandleAsync(EventStoreIndexRequested @event)
    {
        var startRebuildAt = @event.Timebox.RequestStartAt;
        if (startRebuildAt.AddMinutes(5) > DateTime.UtcNow && @event.Timebox.HasExpired == false)
        {
            await RequestTimeoutAsync(new RebuildIndexInternal(@event, @event.Timebox.RequestStartAt, @event.MaxDegreeOfParallelism));
            //RequestTimeout(new EventStoreIndexRebuildTimedout(@event, @event.Timebox.FinishRequestUntil));
        }
    }

    public async Task HandleAsync(RebuildIndexInternal processManagerTimeout)
    {
        IInceptionJob<object> job = null;
        // we need to redesign the job factories
        var theId = processManagerTimeout.EventStoreIndexRequest.Id.Id;

        if (theId.Equals(typeof(MessageCounterIndex).GetContractId(), StringComparison.OrdinalIgnoreCase))
        {
            job = messageCounterJobFactory.CreateJob(processManagerTimeout.EventStoreIndexRequest.Timebox);
        }
        else
        {
            job = jobFactory.CreateJob(processManagerTimeout.EventStoreIndexRequest.Timebox, processManagerTimeout.MaxDegreeOfParallelism);
        }

        JobExecutionStatus result = await jobRunner.ExecuteAsync(job);

        if (result == JobExecutionStatus.Running)
        {
            await RequestTimeoutAsync(new RebuildIndexInternal(processManagerTimeout.EventStoreIndexRequest, DateTime.UtcNow.AddSeconds(60), processManagerTimeout.MaxDegreeOfParallelism));
        }
        else if (result == JobExecutionStatus.Failed)
        {
            // log error
            await RequestTimeoutAsync(new RebuildIndexInternal(processManagerTimeout.EventStoreIndexRequest, DateTime.UtcNow.AddSeconds(60), processManagerTimeout.MaxDegreeOfParallelism));
        }
        else if (result == JobExecutionStatus.Completed)
        {
            var finalize = new FinalizeEventStoreIndexRequest(processManagerTimeout.EventStoreIndexRequest.Id);
            await commandPublisher.PublishAsync(finalize);
        }
    }

    public Task HandleAsync(EventStoreIndexRebuildTimedout processManagerTimeout)
    {
        //var timedout = new TimeoutProjectionVersionRequest(processManagerTimeout.ProjectionVersionRequest.Id, processManagerTimeout.ProjectionVersionRequest.Version, processManagerTimeout.ProjectionVersionRequest.Timebox);
        //commandPublisher.Publish(timedout);
        return Task.CompletedTask;
    }
}

[DataContract(Name = "09d3f870-66f5-4f00-aedd-659b719791fe")]
public sealed class RebuildIndexInternal : ISystemScheduledMessage
{
    RebuildIndexInternal()
    {
        Timestamp = DateTimeOffset.UtcNow;
    }

    public RebuildIndexInternal(EventStoreIndexRequested indexRequest, DateTime publishAt, int maxDegreeOfParallelism) : this()
    {
        EventStoreIndexRequest = indexRequest;
        PublishAt = publishAt;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    [DataMember(Order = 1)]
    public EventStoreIndexRequested EventStoreIndexRequest { get; private set; }

    [DataMember(Order = 2)]
    public DateTimeOffset PublishAt { get; set; }

    [DataMember(Order = 3)]
    public DateTimeOffset Timestamp { get; private set; }

    [DataMember(Order = 4)]
    public int MaxDegreeOfParallelism { get; private set; }

    public string Tenant { get { return EventStoreIndexRequest.Id.Tenant; } }
}

[DataContract(Name = "4f6c585f-31c7-4bcb-867c-2c38071c29f3")]
public sealed class EventStoreIndexRebuildTimedout : ISystemScheduledMessage
{
    EventStoreIndexRebuildTimedout()
    {
        Timestamp = DateTimeOffset.UtcNow;
    }

    public EventStoreIndexRebuildTimedout(EventStoreIndexRequested eventStoreIndexRequest, DateTime publishAt) : this()
    {
        EventStoreIndexRequest = eventStoreIndexRequest;
        PublishAt = publishAt;
    }

    [DataMember(Order = 1)]
    public EventStoreIndexRequested EventStoreIndexRequest { get; private set; }

    [DataMember(Order = 2)]
    public DateTimeOffset PublishAt { get; set; }

    [DataMember(Order = 2)]
    public DateTimeOffset Timestamp { get; private set; }

    public string Tenant { get { return EventStoreIndexRequest.Id.Tenant; } }
}
