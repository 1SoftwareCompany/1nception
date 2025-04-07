using One.Inception.Cluster.Job;
using One.Inception.EventStore.Players;
using One.Inception.Projections.Versioning;

namespace One.Inception.Projections.Rebuilding;

public interface IProjection_JobFactory
{
    IInceptionJob<object> CreateJob(ProjectionVersion version, ReplayEventsOptions replayEventsOptions, VersionRequestTimebox timebox);
}
