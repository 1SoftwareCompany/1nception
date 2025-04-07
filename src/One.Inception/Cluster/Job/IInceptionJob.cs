using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Cluster.Job;

public interface IInceptionJob
{
    /// <summary>
    /// The name of the job
    /// </summary>
    string Name { get; }

    //Task BeforeRunAsync();
    //Task AfterRunAsync();
}

public interface IInceptionJob<out TData> : IInceptionJob
    where TData : class
{
    public TData Data { get; }

    Task SyncInitialStateAsync(IClusterOperations cluster, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the job
    /// </summary>
    Task<JobExecutionStatus> RunAsync(IClusterOperations cluster, CancellationToken cancellationToken = default);
}
