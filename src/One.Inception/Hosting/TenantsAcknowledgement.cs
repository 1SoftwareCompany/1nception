using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.Cluster.Job;
using One.Inception.Multitenancy;

namespace One.Inception;

/// <summary>
/// Every node writes to the cluster which tenants it has configured, so the node which bootstraps a new tenant can wait until all nodes know the tenant.
/// The nodes reload the tenants at different moments, and a node which does not know the tenant yet drops the messages which the bootstrap publishes for it.
/// </summary>
/// <remarks>
/// A node writes its tenants only when they matter: when it starts, right after its tenants change and while it waits for the other nodes. Nothing is written in between.
/// A node which stops removes itself and writes nothing after that. A node which crashes cannot, so a node which has not written a new tenant when the wait for it ends is considered gone and is removed.
/// A node which was only slow writes itself back when its tenants change.
/// </remarks>
internal sealed class TenantsAcknowledgement
{
    internal static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BusyRetryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RemoveOnStopTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<TenantsOptions> monitor;
    private readonly ILogger logger;
    private readonly string node;
    private readonly TimeSpan waitTimeout;
    private readonly TimeSpan pollInterval;
    private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
    private int started;
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

    /// <summary>
    /// Writes the tenants of this node, and removes them when the application stops.
    /// </summary>
    public async Task StartAsync()
    {
        await AcknowledgeAsync().ConfigureAwait(false);

        if (Interlocked.Exchange(ref started, 1) == 1)
            return;

        // The other nodes should not wait for a node which has stopped. The host runs the stopping callbacks before it stops, so the removal is done before the process exits.
        serviceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping.Register(() => Task.Run(RemoveAsync).Wait(RemoveOnStopTimeout));
    }

    /// <summary>
    /// Removes this node. The writes which come after it, for example the writes shortly after a tenants change, are not done, because they would write this node back.
    /// </summary>
    private Task RemoveAsync()
    {
        stopping = true;
        return WriteAsync();
    }

    /// <summary>
    /// Writes the current tenants of this node.
    /// </summary>
    /// <returns>The live nodes and their tenants, or null when the cluster is not available.</returns>
    public Task<IReadOnlyDictionary<string, List<string>>> AcknowledgeAsync() => WriteAsync(monitor.CurrentValue.Tenants.ToList());

    /// <summary>
    /// Writes the tenants of this node right after they change, and a few more times shortly after.
    /// The job runner writes what a node has read before it took the key, so a node which writes at the same moment can overwrite what this node has written.
    /// A lost write only makes the bootstrapping node wait longer, and writing again repairs it within <see cref="DefaultPollInterval"/>.
    /// </summary>
    public async Task AcknowledgeChangeAsync()
    {
        await AcknowledgeAsync().ConfigureAwait(false);

        foreach (int times in new[] { 1, 2, 4 })
        {
            await Task.Delay(pollInterval * times).ConfigureAwait(false);
            await AcknowledgeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until all nodes have the tenant configured, at most <see cref="DefaultWaitTimeout"/>.
    /// The nodes which have not written it by then are removed, because a live node writes its tenants right after they change.
    /// When the nodes cannot be checked, it does not wait.
    /// </summary>
    public async Task WaitUntilAcknowledgedAsync(string tenant)
    {
        DateTime waitUntil = DateTime.UtcNow.Add(waitTimeout);
        DateTime startedAt = DateTime.UtcNow;
        List<string> missing = new List<string>();

        while (true)
        {
            if (monitor.CurrentValue.Tenants.Contains(tenant, StringComparer.OrdinalIgnoreCase) == false)
                return; // the tenant has been removed meanwhile, the other nodes will not get it

            if (stopping)
                return; // this node is going away, it does not write itself anymore

            IReadOnlyDictionary<string, List<string>> nodes = await AcknowledgeAsync().ConfigureAwait(false);
            if (nodes is null)
            {
                if (stopping)
                    return;

                logger.LogWarning("Unable to check which nodes know tenant {tenant}. Bootstrapping it without waiting for them", tenant);
                return;
            }

            missing = nodes.Where(n => n.Value.Contains(tenant, StringComparer.OrdinalIgnoreCase) == false).Select(n => n.Key).ToList();
            if (missing.Count == 0)
            {
                if (DateTime.UtcNow - startedAt > pollInterval)
                    logger.LogInformation("All {count} nodes know tenant {tenant} after {waited}", nodes.Count, tenant, DateTime.UtcNow - startedAt);
                return;
            }

            if (DateTime.UtcNow >= waitUntil)
            {
                logger.LogWarning("Nodes {@nodes} do not know tenant {tenant} after {timeout}. They are considered gone and are removed from the nodes. Bootstrapping the tenant anyway, they may drop its messages", missing, tenant, waitTimeout);
                await WriteAsync(monitor.CurrentValue.Tenants.ToList(), missing).ConfigureAwait(false);
                return;
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Waiting for nodes {@nodes} to know tenant {tenant}", missing, tenant);

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }
    }

    /// <param name="tenants">The tenants of this node, or null to remove this node.</param>
    /// <param name="removeNodes">Other nodes which are gone.</param>
    private async Task<IReadOnlyDictionary<string, List<string>>> WriteAsync(List<string> tenants = null, IReadOnlyCollection<string> removeNodes = null)
    {
        await writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (stopping && tenants is not null)
                return null; // this node has been removed, or is being removed

            DateTime retryUntil = DateTime.UtcNow.Add(BusyRetryTimeout);
            while (true)
            {
                TenantsAcknowledgement_Job job = null;
                JobExecutionStatus status;
                try
                {
                    using (IServiceScope scope = serviceProvider.CreateScope())
                    {
                        IInceptionJobRunner jobRunner = scope.ServiceProvider.GetService<IInceptionJobRunner>();
                        if (jobRunner is null)
                            return null;

                        string boundedContext = scope.ServiceProvider.GetRequiredService<IOptions<BoundedContext>>().Value.Name;
                        job = new TenantsAcknowledgement_Job(boundedContext, node, tenants, removeNodes);
                        status = await jobRunner.ExecuteAsync(job).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to write the tenants of this node to the cluster");
                    return null;
                }

                if (status == JobExecutionStatus.Completed && job.LiveNodes is not null)
                    return job.LiveNodes;

                // Running means another node is writing its tenants right now
                if (status != JobExecutionStatus.Running || DateTime.UtcNow >= retryUntil)
                {
                    logger.LogWarning("Failed to write the tenants of this node to the cluster. Job status: {status}", status);
                    return null;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 200))).ConfigureAwait(false);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }
}

/// <summary>
/// Writes the tenants of one node to the cluster, next to the tenants of the other nodes, and removes the nodes which are gone.
/// </summary>
internal sealed class TenantsAcknowledgement_Job : IInceptionJob<TenantsAcknowledgement_JobData>
{
    private readonly string node;
    private readonly List<string> tenants;
    private readonly IReadOnlyCollection<string> removeNodes;

    /// <param name="tenants">The tenants of the node, or null to remove the node.</param>
    /// <param name="removeNodes">Other nodes which are gone.</param>
    public TenantsAcknowledgement_Job(string boundedContext, string node, List<string> tenants, IReadOnlyCollection<string> removeNodes = null)
    {
        Name = $"urn:{boundedContext}:tenants-acknowledgement";
        this.node = node;
        this.tenants = tenants;
        this.removeNodes = removeNodes ?? Array.Empty<string>();
    }

    public string Name { get; }

    public TenantsAcknowledgement_JobData Data { get; private set; } = new TenantsAcknowledgement_JobData();

    /// <summary>
    /// The live nodes and their tenants after this node has written its tenants.
    /// </summary>
    public IReadOnlyDictionary<string, List<string>> LiveNodes { get; private set; }

    public async Task SyncInitialStateAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        Data = await cluster.PingAsync<TenantsAcknowledgement_JobData>(cancellationToken).ConfigureAwait(false) ?? new TenantsAcknowledgement_JobData();
    }

    public async Task<JobExecutionStatus> RunAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        // Read again now that this node holds the key, another node may have written its tenants after the initial sync
        TenantsAcknowledgement_JobData data = await cluster.PingAsync<TenantsAcknowledgement_JobData>(cancellationToken).ConfigureAwait(false) ?? Data;
        data.Nodes ??= new Dictionary<string, AcknowledgedTenants>();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (tenants is null)
            data.Nodes.Remove(node);
        else
            data.Nodes[node] = new AcknowledgedTenants { Tenants = tenants, LastSeen = now };

        foreach (string gone in removeNodes.Where(gone => gone != node))
            data.Nodes.Remove(gone);

        data.Timestamp = now;
        Data = await cluster.PingAsync(data, cancellationToken).ConfigureAwait(false) ?? data;
        LiveNodes = data.Nodes.ToDictionary(n => n.Key, n => n.Value.Tenants ?? new List<string>());

        return JobExecutionStatus.Completed;
    }
}

internal sealed class TenantsAcknowledgement_JobData : IJobData
{
    public bool IsCompleted { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public Dictionary<string, AcknowledgedTenants> Nodes { get; set; } = new Dictionary<string, AcknowledgedTenants>();
}

internal sealed class AcknowledgedTenants
{
    public List<string> Tenants { get; set; } = new List<string>();

    public DateTimeOffset LastSeen { get; set; }
}
