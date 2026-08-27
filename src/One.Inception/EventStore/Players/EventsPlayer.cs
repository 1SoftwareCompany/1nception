using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using One.Inception.Cluster.Job;
using Microsoft.Extensions.Logging;

namespace One.Inception.EventStore.Players;

[DataContract(Name = "51b93c21-20fb-473f-b7fc-c12e6a56e194")]
public sealed class EventsPlayer : ISystemTrigger,
    ISignalHandle<ReplayPublicEventsRequested>,
    ISignalHandle<ReplayInternalEventsRequested>
{
    private readonly IInceptionJobRunner jobRunner;
    private readonly ReplayPublicEvents_JobFactory publicJobFactory;
    private readonly ReplayInternalEvents_JobFactory internalJobFactory;
    private readonly ILogger<EventsPlayer> logger;

    public EventsPlayer(IInceptionJobRunner jobRunner, ReplayPublicEvents_JobFactory publicJobFactory, ReplayInternalEvents_JobFactory internalJobFactory, ILogger<EventsPlayer> logger)
    {
        this.jobRunner = jobRunner;
        this.publicJobFactory = publicJobFactory;
        this.internalJobFactory = internalJobFactory;
        this.logger = logger;
    }

    public async Task HandleAsync(ReplayPublicEventsRequested signal)
    {
        try
        {
            ReplayPublicEvents_Job job = publicJobFactory.CreateJob(signal);
            JobExecutionStatus result = await jobRunner.ExecuteAsync(job).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Replay public events finished.");
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Failed to replay public events."))) { }
    }

    public async Task HandleAsync(ReplayInternalEventsRequested signal)
    {
        try
        {
            ReplayInternalEvents_Job job = internalJobFactory.CreateJob(signal);
            JobExecutionStatus result = await jobRunner.ExecuteAsync(job).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Replay public events finished.");
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Failed to replay public events."))) { }
    }
}
