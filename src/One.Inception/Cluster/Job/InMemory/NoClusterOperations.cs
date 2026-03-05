using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Cluster.Job.InMemory;

public class NoClusterOperations : IClusterOperations
{
    public Task<TData> PingAsync<TData>(CancellationToken cancellationToken = default) where TData : class, new()
    {
        return Task.FromResult<TData>(default);
    }

    public Task<TData> PingAsync<TData>(TData data, CancellationToken cancellationToken = default) where TData : class, new()
    {
        return Task.FromResult<TData>(data);
    }

    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
