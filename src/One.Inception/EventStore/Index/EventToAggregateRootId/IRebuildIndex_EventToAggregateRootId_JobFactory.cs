using One.Inception.Cluster.Job;
using One.Inception.Projections.Versioning;

namespace One.Inception.EventStore.Index;

public interface IRebuildIndex_EventToAggregateRootId_JobFactory
{
    IInceptionJob<object> CreateJob(VersionRequestTimebox timebox, int maxDegreeOfParallelism);
    string GetJobName();
}
