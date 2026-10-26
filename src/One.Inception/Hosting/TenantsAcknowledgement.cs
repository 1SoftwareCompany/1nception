using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.Cluster;
using One.Inception.Multitenancy;

namespace One.Inception;

/// <summary>
/// Every node registers its tenants in the cluster, so the node which bootstraps a new tenant can wait until all nodes know it. A node which does not know the tenant yet drops its messages.
/// A node which has not registered the tenant when the wait ends is considered gone and is unregistered. A node which was only slow registers itself again when its tenants change.
/// </summary>
internal sealed class TenantsAcknowledgement
{
    internal static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UnregisterOnStopTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<TenantsOptions> monitor;
    private readonly ILogger logger;
    private readonly string node;
    private readonly TimeSpan waitTimeout;
    private readonly TimeSpan pollInterval;
    private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
    private volatile bool stopping;

    public TenantsAcknowledgement(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger logger, string node = null,
        TimeSpan? waitTimeout = null, TimeSpan? pollInterval = null)
    {
        this.serviceProvider = serviceProvider;
        this.monitor = monitor;
        this.logger = logger;
        this.node = node ?? $"{Environment.MachineName}/{Environment.ProcessId}";
        this.waitTimeout = waitTimeout ?? DefaultWaitTimeout;
        this.pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public async Task StartAsync()
    {
        await AcknowledgeAsync().ConfigureAwait(false);

        serviceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(() => Task.Run(UnregisterAsync).Wait(UnregisterOnStopTimeout));
    }

    public Task AcknowledgeAsync()
    {
        return WriteAsync(registry => stopping
            ? Task.CompletedTask // a stopped node must not register itself back
            : registry.RegisterAsync(node, monitor.CurrentValue.Tenants.ToList()));
    }

    private Task UnregisterAsync()
    {
        stopping = true;
        return WriteAsync(registry => registry.UnregisterAsync(node));
    }

    public async Task WaitUntilAcknowledgedAsync(string tenant)
    {
        DateTime startedAt = DateTime.UtcNow;
        DateTime waitUntil = startedAt.Add(waitTimeout);

        while (true)
        {
            if (monitor.CurrentValue.Tenants.Contains(tenant, StringComparer.OrdinalIgnoreCase) == false)
                return; // the tenant has been removed meanwhile, the other nodes will not get it

            if (stopping)
                return;

            IReadOnlyDictionary<string, IReadOnlyCollection<string>> nodes = await ReadAsync().ConfigureAwait(false);
            if (nodes is null)
                return;

            List<string> missing = nodes
                .Where(other => other.Key != node && other.Value.Contains(tenant, StringComparer.OrdinalIgnoreCase) == false)
                .Select(other => other.Key)
                .ToList();

            if (missing.Count == 0)
            {
                if (DateTime.UtcNow - startedAt > pollInterval)
                    logger.LogInformation("All {count} nodes know tenant {tenant} after {waited}", nodes.Count, tenant, DateTime.UtcNow - startedAt);
                return;
            }

            if (DateTime.UtcNow >= waitUntil)
            {
                logger.LogWarning("Nodes {@nodes} do not know tenant {tenant} after {timeout}. They are considered gone and are removed from the nodes. Bootstrapping the tenant anyway, they may drop its messages", missing, tenant, waitTimeout);
                foreach (string gone in missing)
                {
                    await WriteAsync(registry => registry.UnregisterAsync(gone)).ConfigureAwait(false);
                }
                return;
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Waiting for nodes {@nodes} to know tenant {tenant}", missing, tenant);

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ReadAsync()
    {
        ITenantsRegistry registry = serviceProvider.GetService<ITenantsRegistry>();
        if (registry is null)
            return null;

        try
        {
            return await registry.GetAllAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to check which nodes know the tenants. Bootstrapping without waiting for them");
            return null;
        }
    }

    private async Task WriteAsync(Func<ITenantsRegistry, Task> write)
    {
        ITenantsRegistry registry = serviceProvider.GetService<ITenantsRegistry>();
        if (registry is null)
            return;

        await writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await write(registry).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write the tenants of this node to the cluster");
        }
        finally
        {
            writeLock.Release();
        }
    }
}
