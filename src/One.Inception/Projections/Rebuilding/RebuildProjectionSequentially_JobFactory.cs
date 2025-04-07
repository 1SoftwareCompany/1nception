using One.Inception.Cluster.Job;
using One.Inception.EventStore.Players;
using One.Inception.MessageProcessing;
using One.Inception.Projections.Versioning;
using Microsoft.Extensions.Options;

namespace One.Inception.Projections.Rebuilding;

public class RebuildProjectionSequentially_JobFactory : IProjection_JobFactory
{
    private readonly RebuildProjectionSequentially_Job job;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly BoundedContext boundedContext;

    public RebuildProjectionSequentially_JobFactory(RebuildProjectionSequentially_Job job, IOptions<BoundedContext> boundedContext, IInceptionContextAccessor contextAccessor)
    {
        this.job = job;
        this.contextAccessor = contextAccessor;
        this.boundedContext = boundedContext.Value;
    }

    public IInceptionJob<object> CreateJob(ProjectionVersion version, ReplayEventsOptions replayEventsOptions, VersionRequestTimebox timebox)
    {
        job.Name = $"urn:{boundedContext.Name}:{contextAccessor.Context.Tenant}:{job.Name}:{version.ProjectionName}_{version.Hash}_{version.Revision}";

        job.BuildInitialData(() => new RebuildProjectionSequentially_JobData()
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
