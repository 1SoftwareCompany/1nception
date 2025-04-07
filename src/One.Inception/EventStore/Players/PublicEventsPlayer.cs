using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using One.Inception.Cluster.Job;
using Microsoft.Extensions.Logging;

namespace One.Inception.EventStore.Players;

[DataContract(Name = "51b93c21-20fb-473f-b7fc-c12e6a56e194")]
public sealed class PublicEventsPlayer : ISystemTrigger,
    ISignalHandle<ReplayPublicEventsRequested>
{
    private readonly IInceptionJobRunner jobRunner;
    private readonly ReplayPublicEvents_JobFactory jobFactory;
    private readonly ILogger<PublicEventsPlayer> logger;

    public PublicEventsPlayer(IInceptionJobRunner jobRunner, ReplayPublicEvents_JobFactory jobFactory, ILogger<PublicEventsPlayer> logger)
    {
        this.jobRunner = jobRunner;
        this.jobFactory = jobFactory;
        this.logger = logger;
    }

    public async Task HandleAsync(ReplayPublicEventsRequested signal)
    {
        try
        {
            ReplayPublicEvents_Job job = jobFactory.CreateJob(signal);
            JobExecutionStatus result = await jobRunner.ExecuteAsync(job).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Replay public events finished.");
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Failed to replay public events."))) { }
    }
}
