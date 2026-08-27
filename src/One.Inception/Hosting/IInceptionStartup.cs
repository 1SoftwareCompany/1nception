using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception;

/// <summary>
/// This type of startups are singleton and are executed ONLY once, so use accordingly
/// </summary>
public interface IInceptionStartup
{
    /// <summary>
    /// bootstrap all tenants (presumably)
    /// </summary>
    /// <returns></returns>
    Task BootstrapAsync();

    /// <summary>
    /// bootstrap only the tenants inside the tenants collection
    /// </summary>
    /// <param name="tenants"></param>
    /// <returns></returns>
    Task BootstrapAsync(IEnumerable<string> tenants);
}

/// <summary>
/// This type of startups are executed X amount of times per tenant, so use accordingly
/// </summary>
public interface ITenantStartup
{
    Task BootstrapAsync();
}
