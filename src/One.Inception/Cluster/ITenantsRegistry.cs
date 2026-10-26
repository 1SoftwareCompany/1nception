using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception.Cluster;

/// <summary>
/// Keeps the tenants of each node of the bounded context in the cluster, one entry per node.
/// </summary>
public interface ITenantsRegistry
{
    Task RegisterAsync(string node, IEnumerable<string> tenants);

    Task UnregisterAsync(string node);

    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetAllAsync();
}
