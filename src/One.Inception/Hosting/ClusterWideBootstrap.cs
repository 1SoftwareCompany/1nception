using System;
using System.Linq;
using System.Runtime.ExceptionServices;
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
/// Runs the cluster wide startups of a tenant on a single node. The other nodes wait for it.
/// If the nodes cannot coordinate (no cluster job runner, the cluster is not reachable or the wait times out), the startups run on this node.
/// </summary>
internal sealed class ClusterWideBootstrap
{
    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<TenantsOptions> monitor;
    private readonly ILogger logger;
    private readonly TenantsAcknowledgement acknowledgement;
    private readonly TimeSpan pollInterval;
    private readonly TimeSpan timeout;
    private DateTimeOffset startedAt;

    public ClusterWideBootstrap(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger logger, TenantsAcknowledgement acknowledgement, TimeSpan pollInterval, TimeSpan timeout)
    {
        this.serviceProvider = serviceProvider;
        this.monitor = monitor;
        this.logger = logger;
        this.acknowledgement = acknowledgement;
        this.pollInterval = pollInterval;
        this.timeout = timeout;
    }

    public Task StartAsync()
    {
        startedAt = DateTimeOffset.UtcNow;
        return acknowledgement.StartAsync();
    }

    public void AcknowledgeTenantsChange() => _ = acknowledgement.AcknowledgeAsync();

    public async Task BootstrapAsync(string tenant, bool isAdded, Func<Task> bootstrap)
    {
        DateTime waitUntil = DateTime.UtcNow.Add(timeout);
        CancellationToken stopping = serviceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;

        while (stopping.IsCancellationRequested == false)
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
                    }, isAdded, startedAt);

                    // a stopping node stops waiting, so the scope disposes the job runner and it releases the job to the other nodes
                    status = await jobRunner.ExecuteAsync(job, stopping).WaitAsync(stopping).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                logger.LogInformation("The node is stopping. The bootstrap of tenant {tenant} is left to the other nodes", tenant);
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run the cluster bootstrap job for tenant {tenant}", tenant);
                status = JobExecutionStatus.Failed;
            }

            if (job?.BootstrapFailure is not null)
            {
                if (job.IsFailureKept == false)
                    ExceptionDispatchInfo.Throw(job.BootstrapFailure);

                logger.LogError(job.BootstrapFailure, "Failed to bootstrap tenant {tenant}. The failure is kept in the cluster, so the other nodes do not run its cluster wide startups again. The node keeps running", tenant);
                return;
            }

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
                logger.LogWarning("Tenant {tenant} was not bootstrapped by another node within {timeout}. Bootstrapping it on this node", tenant, timeout);
                break;
            }

            logger.LogInformation("Tenant {tenant} is being bootstrapped by another node. Waiting for it to finish", tenant);
            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        if (stopping.IsCancellationRequested == false)
            await bootstrap().ConfigureAwait(false);
    }

    public async Task ForgetAsync(string tenant)
    {
        DateTime waitUntil = DateTime.UtcNow.Add(timeout);

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

            // Running means another node is bootstrapping the tenant right now, its completed state must be removed after it finishes
            if (status != JobExecutionStatus.Running || DateTime.UtcNow >= waitUntil)
            {
                logger.LogWarning("Failed to forget the cluster bootstrap of removed tenant {tenant}. Job status: {status}. If the tenant is added back, the cluster wide startups will not run for it until another build is deployed", tenant, status);
                return;
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }
    }

    private bool IsConfigured(string tenant) => monitor.CurrentValue.Tenants.Contains(tenant);
}
