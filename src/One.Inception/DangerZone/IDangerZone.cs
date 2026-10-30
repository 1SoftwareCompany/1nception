using System.Threading.Tasks;

namespace One.Inception.DangerZone;

/// <summary>
/// Defines a data cleanup operation for a specific tenant.
/// Implement this interface to delete a small, focused set of data owned by your component.
/// When invoked, DangerZoneExecutor discovers and executes all registered implementations.
/// </summary>
/// <remarks>
/// Implementations must delete only data belonging to the specified tenant and must not affect other tenants.
/// </remarks>
public interface IDangerZone
{
    /// <summary>
    /// Deletes the data managed by this implementation for the specified tenant.
    /// </summary>
    /// <param name="tenant">The identifier of the tenant whose data should be deleted.</param>
    /// <returns>A task that completes when the deletion has finished.</returns>
    Task WipeDataAsync(string tenant);
}
