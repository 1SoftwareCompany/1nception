using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.Cluster.Job;
using One.Inception.MessageProcessing;
using One.Inception.Multitenancy;

namespace One.Inception;

public sealed class Booter
{
    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<TenantsOptions> monitor;
    private TenantsOptions tenantOptions;
    private readonly ILogger<Booter> logger;
    private static readonly TimeSpan WaitWarningInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] DefaultBootstrapRetryDelays = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)];
    private static readonly TimeSpan DefaultClusterBootstrapPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultClusterBootstrapTimeout = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim tenantsChangeLock = new SemaphoreSlim(1, 1);
    private readonly TimeSpan[] bootstrapRetryDelays;
    private readonly TimeSpan clusterBootstrapPollInterval;
    private readonly TimeSpan clusterBootstrapTimeout;
    private readonly TenantsAcknowledgement acknowledgement;

    private readonly object tenantsChangesSync = new object();
    private TenantsOptions lastNotifiedOptions;
    private readonly HashSet<string> tenantsRemovedSinceLastChange = new HashSet<string>();
    private int pendingTenantsChanges;

    public Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger)
        : this(serviceProvider, monitor, logger, DefaultBootstrapRetryDelays) { }

    internal Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger, TimeSpan[] bootstrapRetryDelays, TimeSpan? clusterBootstrapPollInterval = null, TimeSpan? clusterBootstrapTimeout = null, TenantsAcknowledgement acknowledgement = null)
    {
        this.serviceProvider = serviceProvider;
        this.monitor = monitor;
        this.tenantOptions = monitor.CurrentValue;
        this.logger = logger;
        this.bootstrapRetryDelays = bootstrapRetryDelays;
        this.clusterBootstrapPollInterval = clusterBootstrapPollInterval ?? DefaultClusterBootstrapPollInterval;
        this.clusterBootstrapTimeout = clusterBootstrapTimeout ?? DefaultClusterBootstrapTimeout;
        this.lastNotifiedOptions = tenantOptions;
        this.acknowledgement = acknowledgement ?? new TenantsAcknowledgement(serviceProvider, monitor, logger);

        monitor.OnChange(async newOptions => await OnTenantsOptionsChanged(newOptions).ConfigureAwait(false));
    }

    /// <summary>
    /// True when there is no tenants change waiting or being applied.
    /// </summary>
    internal bool IsIdle => Volatile.Read(ref pendingTenantsChanges) == 0;

    /// <summary>
    /// The tenants which have been bootstrapped on this node.
    /// </summary>
    internal IEnumerable<string> BootstrappedTenants => tenantOptions.Tenants;

    public async Task BootstrapInceptionAsync()
    {
        // Before the consumers start, so a node which bootstraps a new tenant knows about this node
        await acknowledgement.StartAsync().ConfigureAwait(false);

        await tenantsChangeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            tenantOptions = monitor.CurrentValue;
            lock (tenantsChangesSync)
            {
                tenantsRemovedSinceLastChange.ExceptWith(tenantOptions.Tenants); // they are bootstrapped right now
            }

            await BootstrapAllTenantsAsync().ConfigureAwait(false);
        }
        finally
        {
            tenantsChangeLock.Release();
        }
    }

    /// <summary>
    /// The node startups run on this node for all tenants. The cluster wide startups run for each tenant on a single node of this build, the tenants in parallel.
    /// </summary>
    private async Task BootstrapAllTenantsAsync()
    {
        RegisterContracts();

        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        (List<Type> nodeStartupsBefore, List<Type> clusterWideStartups, List<Type> nodeStartupsAfter) = SplitStartups(scanner.Scan());

        foreach (var startupType in nodeStartupsBefore)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            await startup.BootstrapAsync().ConfigureAwait(false);
        }

        if (clusterWideStartups.Count > 0)
            await Task.WhenAll(tenantOptions.Tenants.Select(tenant => BootstrapClusterWideStartupsAsync(clusterWideStartups, tenant))).ConfigureAwait(false);

        foreach (var startupType in nodeStartupsAfter)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            await startup.BootstrapAsync().ConfigureAwait(false);
        }

        IEnumerable<Type> tenantStartups = scanner.ScanForTenantStartups();
        foreach (string tenant in tenantOptions.Tenants)
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

    private async Task OnTenantsOptionsChanged(TenantsOptions newOptions)
    {
        Interlocked.Increment(ref pendingTenantsChanges);
        try
        {
            TrackRemovedTenants(newOptions);

            await Task.Yield(); // lets the other listeners, such as the DefaultContextFactory, see the new tenants before they are bootstrapped
            _ = acknowledgement.AcknowledgeChangeAsync(); // right away, a node which bootstraps a new tenant may be waiting for this node
            await ApplyTenantsChangeAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref pendingTenantsChanges);
        }
    }

    /// <summary>
    /// A tenant can be removed and added back while this node is busy with a previous change. Comparing only the latest tenants with
    /// the bootstrapped ones would miss that, so the removed tenants are remembered until the next change is applied.
    /// </summary>
    private void TrackRemovedTenants(TenantsOptions newOptions)
    {
        if (newOptions is null)
            return;

        lock (tenantsChangesSync)
        {
            tenantsRemovedSinceLastChange.UnionWith(lastNotifiedOptions.Tenants.Except(newOptions.Tenants));
            lastNotifiedOptions = newOptions;
        }
    }

    private List<string> TakeRemovedTenants()
    {
        lock (tenantsChangesSync)
        {
            List<string> removed = tenantsRemovedSinceLastChange.ToList();
            tenantsRemovedSinceLastChange.Clear();
            return removed;
        }
    }

    private void RestoreRemovedTenants(IEnumerable<string> removed)
    {
        lock (tenantsChangesSync)
        {
            tenantsRemovedSinceLastChange.UnionWith(removed);
        }
    }

    private async Task ApplyTenantsChangeAsync()
    {
        while (await tenantsChangeLock.WaitAsync(WaitWarningInterval).ConfigureAwait(false) == false)
        {
            logger.LogWarning("Still waiting for a previous tenant bootstrap to finish before applying the tenants change");
        }

        Exception bootstrapFailure = null;
        try
        {
            TenantsOptions newOptions = monitor.CurrentValue;
            List<string> removedMeanwhile = TakeRemovedTenants();

            List<string> removedTenants = tenantOptions.Tenants.Except(newOptions.Tenants).Union(removedMeanwhile).ToList();
            List<string> newTenants = newOptions.Tenants.Except(tenantOptions.Tenants).Union(removedMeanwhile.Intersect(newOptions.Tenants)).ToList();

            if (removedTenants.Count > 0 || newTenants.Count > 0 || tenantOptions.Tenants.SequenceEqual(newOptions.Tenants) == false)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("tenant options re-loaded with {@options}", newOptions);

                await ForgetClusterBootstrapOfRemovedTenantsAsync(removedTenants).ConfigureAwait(false);

                if (newTenants.Count > 0)
                    bootstrapFailure = await TryBootstrapNewlyStartedTenantsAsync(newTenants).ConfigureAwait(false);

                if (bootstrapFailure is null)
                    tenantOptions = newOptions;
                else
                    RestoreRemovedTenants(removedMeanwhile);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply the tenants change");
        }
        finally
        {
            tenantsChangeLock.Release();
        }

        if (bootstrapFailure is not null)
            RestartApplication(bootstrapFailure);
    }

    private async Task<Exception> TryBootstrapNewlyStartedTenantsAsync(List<string> newTenants)
    {
        for (int attempt = 0; ; attempt++)
        {
            // A tenant can be removed while it is being bootstrapped. The next tenants change takes care of it.
            List<string> tenants = newTenants.Where(IsConfigured).ToList();
            if (tenants.Count == 0)
                return null;

            try
            {
                await BootstrapNewlyStartedTenantsAsync(tenants).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex) when (attempt < bootstrapRetryDelays.Length)
            {
                logger.LogWarning(ex, "Failed to bootstrap new tenants {@tenants}. Retrying in {delay}", tenants, bootstrapRetryDelays[attempt]);
                await Task.Delay(bootstrapRetryDelays[attempt]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (tenants.Any(IsConfigured) == false)
                    return null;

                return ex;
            }
        }
    }

    private bool IsConfigured(string tenant) => monitor.CurrentValue.Tenants.Contains(tenant);

    private void RestartApplication(Exception bootstrapFailure)
    {
        logger.LogCritical(bootstrapFailure, "Failed to bootstrap new tenants after {attempts} attempts. Stopping the application so it restarts and bootstraps all tenants", bootstrapRetryDelays.Length + 1);

        IHostApplicationLifetime lifetime = serviceProvider.GetService<IHostApplicationLifetime>();
        if (lifetime is null)
            ExceptionDispatchInfo.Throw(bootstrapFailure);

        lifetime.StopApplication();
    }

    private async Task BootstrapNewlyStartedTenantsAsync(List<string> newTenants)
    {
        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        (List<Type> nodeStartupsBefore, List<Type> clusterWideStartups, List<Type> nodeStartupsAfter) = SplitStartups(scanner.Scan());

        await BootstrapStartupsAsync(nodeStartupsBefore, newTenants).ConfigureAwait(false);

        if (clusterWideStartups.Count > 0)
        {
            foreach (string tenant in newTenants.Where(IsConfigured))
            {
                await BootstrapClusterWideStartupsAsync(clusterWideStartups, tenant).ConfigureAwait(false);
            }
        }

        await BootstrapStartupsAsync(nodeStartupsAfter, newTenants).ConfigureAwait(false);

        IEnumerable<Type> tenantStartups = scanner.ScanForTenantStartups();
        foreach (string tenant in newTenants.Where(IsConfigured))
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
    /// The types are found by their contract id, for example the projection of a projection version, through a cache which is filled when the contract id of a type is read.
    /// The bootstrap of the tenants used to fill it on every node as a side effect, but a node which skips the bootstrap because another node has done it must fill it too.
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

    /// <summary>
    /// The startups are ordered by rank. The node startups ranked before the first cluster wide startup run first, the rest of the node startups run after the cluster wide ones.
    /// </summary>
    private static (List<Type> NodeStartupsBefore, List<Type> ClusterWideStartups, List<Type> NodeStartupsAfter) SplitStartups(IEnumerable<Type> scanned)
    {
        List<Type> startups = scanned.ToList();
        List<Type> nodeStartupsBefore = startups.TakeWhile(startup => StartupScanner.IsClusterWide(startup) == false).ToList();
        List<Type> clusterWideStartups = startups.Where(StartupScanner.IsClusterWide).ToList();
        List<Type> nodeStartupsAfter = startups.Except(nodeStartupsBefore).Except(clusterWideStartups).ToList();

        return (nodeStartupsBefore, clusterWideStartups, nodeStartupsAfter);
    }

    private async Task BootstrapStartupsAsync(IEnumerable<Type> startups, IEnumerable<string> tenants)
    {
        foreach (var startupType in startups)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            await startup.BootstrapAsync(tenants).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Only one node of this build runs the cluster wide startups for the tenant. The other nodes wait until it finishes.
    /// Before it runs them, it waits until all nodes know the tenant, because the startups publish messages for the tenant which any node may handle.
    /// If the nodes cannot coordinate (no cluster job runner, the cluster is not reachable or the wait times out), the startups run on this node.
    /// </summary>
    private async Task BootstrapClusterWideStartupsAsync(List<Type> clusterWideStartups, string tenant)
    {
        Func<Task> bootstrap = () => BootstrapStartupsAsync(clusterWideStartups, [tenant]);
        DateTime waitUntil = DateTime.UtcNow.Add(clusterBootstrapTimeout);

        while (true)
        {
            TenantBootstrap_Job job = null;
            JobExecutionStatus status;
            try
            {
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    IInceptionJobRunner jobRunner = scope.ServiceProvider.GetService<IInceptionJobRunner>();
                    TenantBootstrap_JobFactory jobFactory = scope.ServiceProvider.GetService<TenantBootstrap_JobFactory>();
                    if (jobRunner is null || jobFactory is null)
                    {
                        await bootstrap().ConfigureAwait(false);
                        return;
                    }

                    job = jobFactory.CreateJob(tenant, async () =>
                    {
                        await acknowledgement.WaitUntilAcknowledgedAsync(tenant).ConfigureAwait(false);
                        if (IsConfigured(tenant) == false)
                            return false; // removed meanwhile

                        await bootstrap().ConfigureAwait(false);
                        return IsConfigured(tenant);
                    });
                    status = await jobRunner.ExecuteAsync(job).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run the cluster bootstrap job for tenant {tenant}", tenant);
                status = JobExecutionStatus.Failed;
            }

            if (job?.BootstrapFailure is not null)
                ExceptionDispatchInfo.Throw(job.BootstrapFailure);

            if (status == JobExecutionStatus.Completed || job?.HasBootstrapped == true)
                return;

            if (status != JobExecutionStatus.Running)
            {
                logger.LogWarning("Could not coordinate the bootstrap of tenant {tenant} with the other nodes. Job status: {status}. Bootstrapping it on this node", tenant, status);
                break;
            }

            if (IsConfigured(tenant) == false)
                return; // the tenant has been removed while waiting

            if (DateTime.UtcNow >= waitUntil)
            {
                logger.LogWarning("Tenant {tenant} was not bootstrapped by another node within {timeout}. Bootstrapping it on this node", tenant, clusterBootstrapTimeout);
                break;
            }

            logger.LogInformation("Tenant {tenant} is being bootstrapped by another node. Waiting for it to finish", tenant);
            await Task.Delay(clusterBootstrapPollInterval).ConfigureAwait(false);
        }

        await bootstrap().ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the cluster bootstrap state of the removed tenants, so they are bootstrapped again if they are added back.
    /// </summary>
    private async Task ForgetClusterBootstrapOfRemovedTenantsAsync(List<string> removedTenants)
    {
        foreach (string tenant in removedTenants)
        {
            await ForgetClusterBootstrapAsync(tenant).ConfigureAwait(false);
        }
    }

    private async Task ForgetClusterBootstrapAsync(string tenant)
    {
        DateTime waitUntil = DateTime.UtcNow.Add(clusterBootstrapTimeout);

        while (true)
        {
            JobExecutionStatus status;
            try
            {
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    IInceptionJobRunner jobRunner = scope.ServiceProvider.GetService<IInceptionJobRunner>();
                    TenantBootstrap_JobFactory jobFactory = scope.ServiceProvider.GetService<TenantBootstrap_JobFactory>();
                    if (jobRunner is null || jobFactory is null)
                        return;

                    status = await jobRunner.ExecuteAsync(jobFactory.CreateForgetJob(tenant)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to forget the cluster bootstrap of removed tenant {tenant}. If the tenant is added back, the cluster wide startups will not run for it until another build is deployed", tenant);
                return;
            }

            if (status == JobExecutionStatus.Completed)
                return;

            // Running means another node is bootstrapping or forgetting the tenant right now. Its completed state must be removed after it finishes.
            if (status != JobExecutionStatus.Running || DateTime.UtcNow >= waitUntil)
            {
                logger.LogWarning("Failed to forget the cluster bootstrap of removed tenant {tenant}. Job status: {status}. If the tenant is added back, the cluster wide startups will not run for it until another build is deployed", tenant, status);
                return;
            }

            await Task.Delay(clusterBootstrapPollInterval).ConfigureAwait(false);
        }
    }
}
