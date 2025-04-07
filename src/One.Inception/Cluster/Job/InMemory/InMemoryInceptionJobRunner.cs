using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Cluster.Job.InMemory;

public sealed class InMemoryInceptionJobRunner : IInceptionJobRunner
{
    static NoClusterOperations clusterOperations = new NoClusterOperations();

    public JobManager JobManager => throw new System.NotImplementedException();

    public void Dispose()
    {

    }

    public Task<JobExecutionStatus> ExecuteAsync(IInceptionJob<object> job, CancellationToken cancellationToken = default)
    {
        return job.RunAsync(clusterOperations, cancellationToken);
    }
}
