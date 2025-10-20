using System.Threading.Tasks;

namespace One.Inception;

/// <summary>
/// This type of startups are singleton and are executed ONLY once, so use accordingly
/// </summary>
public interface IInceptionStartup
{
    // TODO: Make this async
    Task BootstrapAsync();
}

/// <summary>
/// This type of startups are executed X amount of times per tenant, so use accordingly
/// </summary>
public interface ITenantStartup //TODO: also make this async :) kali
{
    Task BootstrapAsync();
}
