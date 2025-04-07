using One.Inception.Cluster.Job;
using One.Inception.EventStore.Players;
using One.Inception.MessageProcessing;
using One.Inception.Projections.Versioning;
using Microsoft.Extensions.Options;

namespace One.Inception.Projections.Rebuilding;

public class RebuildProjection_JobFactory : IProjection_JobFactory
{
    private readonly RebuildProjection_Job job;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly BoundedContext boundedContext;

    public RebuildProjection_JobFactory(RebuildProjection_Job job, IOptions<BoundedContext> boundedContext, IInceptionContextAccessor contextAccessor)
    {
        this.job = job;
        this.contextAccessor = contextAccessor;
        this.boundedContext = boundedContext.Value;
    }

    public IInceptionJob<object> CreateJob(ProjectionVersion version, ReplayEventsOptions replayEventsOptions, VersionRequestTimebox timebox)
    {
        job.Name = $"urn:{contextAccessor.Context.Tenant}:{boundedContext.Name}:{job.Name}:{version.ProjectionName}_{version.Hash}_{version.Revision}";

        job.BuildInitialData(() => new RebuildProjection_JobData()
        {
            After = replayEventsOptions.After,
            Before = replayEventsOptions.Before,
            MaxDegreeOfParallelism = replayEventsOptions.MaxDegreeOfParallelism,
            Timestamp = timebox.RequestStartAt,
            DueDate = timebox.FinishRequestUntil,
            Version = version

        });

        return job;
    }
}
