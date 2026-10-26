using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.MessageProcessing;
using One.Inception.Multitenancy;

namespace One.Inception;

public sealed class Booter
{
    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<TenantsOptions> monitor;
    private TenantsOptions tenantOptions;
    private readonly ILogger<Booter> logger;
    private readonly ClusterWideBootstrap clusterWideBootstrap;
    private readonly SemaphoreSlim tenantsChangeLock = new SemaphoreSlim(1, 1);
    private int pendingTenantsChanges;

    public Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger)
        : this(serviceProvider, monitor, logger, new ClusterWideBootstrap(serviceProvider, monitor, logger, new TenantsAcknowledgement(serviceProvider, monitor, logger), pollInterval: TimeSpan.FromSeconds(10), timeout: TimeSpan.FromMinutes(5))) { }

    internal Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger, ClusterWideBootstrap clusterWideBootstrap)
    {
        this.serviceProvider = serviceProvider;
        this.monitor = monitor;
        this.tenantOptions = monitor.CurrentValue;
        this.logger = logger;
        this.clusterWideBootstrap = clusterWideBootstrap;

        monitor.OnChange(async newOptions => await OnTenantsOptionsChanged(newOptions).ConfigureAwait(false));
    }

    internal bool IsIdle => Volatile.Read(ref pendingTenantsChanges) == 0;

    internal IEnumerable<string> BootstrappedTenants => tenantOptions.Tenants;

    public async Task BootstrapInceptionAsync()
    {
        await clusterWideBootstrap.StartAsync().ConfigureAwait(false);

        await tenantsChangeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await BootstrapAllTenantsAsync(tenantOptions.Tenants).ConfigureAwait(false);
        }
        finally
        {
            tenantsChangeLock.Release();
        }
    }

    private async Task BootstrapAllTenantsAsync(IEnumerable<string> tenants)
    {
        RegisterContracts();

        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        var clusterWideStartups = scanner.ScanForClusterWideStartups().ToList();

        await BootstrapStartupsAsync(scanner.ScanForNodeStartupsBeforeClusterWide()).ConfigureAwait(false);
        await Task.WhenAll(tenants.Select(tenant => BootstrapClusterWideStartupsAsync(clusterWideStartups, tenant, isAdded: false))).ConfigureAwait(false);
        await BootstrapStartupsAsync(scanner.ScanForNodeStartupsAfterClusterWide()).ConfigureAwait(false);

        await BootstrapTenantStartupsAsync(scanner, tenants.Where(IsConfigured)).ConfigureAwait(false); // a tenant can be removed while the cluster wide startups wait for another node
    }

    private async Task OnTenantsOptionsChanged(TenantsOptions newOptions)
    {
        TenantsOptions oldOptions = Interlocked.Exchange(ref tenantOptions, newOptions);
        var newTenants = newOptions.Tenants.Except(oldOptions.Tenants).ToArray();
        var removedTenants = oldOptions.Tenants.Except(newOptions.Tenants).ToArray();
        if (newTenants.Length == 0 && removedTenants.Length == 0)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("tenant options re-loaded with {@options}", newOptions);

        Interlocked.Increment(ref pendingTenantsChanges);
        clusterWideBootstrap.AcknowledgeTenantsChange();
        await Task.Yield(); // lets the other listeners, such as the DefaultContextFactory, see the new tenants before they are bootstrapped

        await tenantsChangeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (string tenant in removedTenants)
            {
                await clusterWideBootstrap.ForgetAsync(tenant).ConfigureAwait(false);
            }

            await BootstrapNewlyStartedTenantsAsync(newTenants).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Not retried, the service which adds the tenant handles the failure
            logger.LogError(ex, "Failed to bootstrap new tenants {@tenants}. The service keeps running", newTenants);
        }
        finally
        {
            tenantsChangeLock.Release();
            Interlocked.Decrement(ref pendingTenantsChanges);
        }
    }

    private async Task BootstrapNewlyStartedTenantsAsync(IEnumerable<string> newTenants)
    {
        var tenants = newTenants.Where(IsConfigured).ToArray(); // a tenant can be removed meanwhile, the next tenants change takes care of it
        if (tenants.Length == 0)
            return;

        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        var clusterWideStartups = scanner.ScanForClusterWideStartups().ToList();

        await BootstrapStartupsAsync(scanner.ScanForNodeStartupsBeforeClusterWide(), tenants).ConfigureAwait(false);
        foreach (string tenant in tenants.Where(IsConfigured))
        {
            await BootstrapClusterWideStartupsAsync(clusterWideStartups, tenant, isAdded: true).ConfigureAwait(false);
        }
        await BootstrapStartupsAsync(scanner.ScanForNodeStartupsAfterClusterWide(), tenants).ConfigureAwait(false);

        await BootstrapTenantStartupsAsync(scanner, tenants.Where(IsConfigured)).ConfigureAwait(false);
    }

    private bool IsConfigured(string tenant) => monitor.CurrentValue.Tenants.Contains(tenant);

    private Task BootstrapClusterWideStartupsAsync(List<Type> clusterWideStartups, string tenant, bool isAdded)
    {
        if (clusterWideStartups.Count == 0)
            return Task.CompletedTask;

        return clusterWideBootstrap.BootstrapAsync(tenant, isAdded, () => BootstrapStartupsAsync(clusterWideStartups, [tenant]));
    }

    private async Task BootstrapTenantStartupsAsync(StartupScanner scanner, IEnumerable<string> tenants)
    {
        IEnumerable<Type> tenantStartups = scanner.ScanForTenantStartups();
        foreach (string tenant in tenants)
        {
            using (var scopedServiceProvider = serviceProvider.CreateScope())
            {
                DefaultContextFactory contextFactory = scopedServiceProvider.ServiceProvider.GetRequiredService<DefaultContextFactory>();
                InceptionContext context = contextFactory.Create(tenant, scopedServiceProvider.ServiceProvider);

                foreach (var tenantStartupType in tenantStartups)
                {
                    ITenantStartup tenantStartUp = (ITenantStartup)context.ServiceProvider.GetRequiredService(tenantStartupType);
                    await tenantStartUp.BootstrapAsync().ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// The cluster wide startups fill the contracts cache as a side effect, so a node which skips them must fill it itself.
    /// </summary>
    private void RegisterContracts()
    {
        foreach (Type type in new DefaulAssemblyScanner().Scan())
        {
            if (type.IsGenericTypeDefinition || string.IsNullOrEmpty(type.GetCustomAttribute<DataContractAttribute>(false)?.Name))
                continue;

            try
            {
                type.GetContractId();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to register the contract of {type}", type.FullName);
            }
        }
    }

    private async Task BootstrapStartupsAsync(IEnumerable<Type> startups)
    {
        foreach (var startupType in startups)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            await startup.BootstrapAsync().ConfigureAwait(false);
        }
    }

    private async Task BootstrapStartupsAsync(IEnumerable<Type> startups, IEnumerable<string> tenants)
    {
        foreach (var startupType in startups)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            await startup.BootstrapAsync(tenants).ConfigureAwait(false);
        }
    }
}
