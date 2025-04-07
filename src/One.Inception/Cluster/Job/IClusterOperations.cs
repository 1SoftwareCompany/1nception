using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Cluster.Job;

/// <summary>
/// Defines operations which could be executed against the cluster.
/// </summary>
/// <remarks>Consider implementing <see cref="IInceptionJobRunner"></see> as well.</remarks>
public interface IClusterOperations
{
    /// <summary>
    /// Sends a heart beat message to the cluster.
    /// </summary>
    /// <typeparam name="TData">The data.</typeparam>
    /// <returns>Returns the state from the cluster. Null if there is no state.</returns>
    Task<TData> PingAsync<TData>(CancellationToken cancellationToken = default) where TData : class, new();

    /// <summary>
    /// Sends a heart beat message to the cluster with the data which will be stored there.
    /// </summary>
    /// <typeparam name="TData">The data.</typeparam>
    /// <returns>Returns the state from the cluster. Null if there is no state.</returns>
    Task<TData> PingAsync<TData>(TData data, CancellationToken cancellationToken = default) where TData : class, new();
}
