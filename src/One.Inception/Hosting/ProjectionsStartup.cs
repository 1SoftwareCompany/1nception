using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.EventStore.Players;
using One.Inception.Multitenancy;
using One.Inception.Projections;
using One.Inception.Projections.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace One.Inception;

[InceptionStartup(Bootstraps.Projections)]
internal sealed class ProjectionsStartup : IInceptionStartup
{
    private readonly IServiceProvider serviceProvider;
    private readonly ProjectionFinderViaReflection projectionFinderViaReflection;
    private readonly IPublisher<ICommand> publisher;
    private readonly ILogger<ProjectionsStartup> logger;
    private InceptionHostOptions hostOptions;
    private TenantsOptions tenants;

    public ProjectionsStartup(IServiceProvider serviceProvider, ProjectionFinderViaReflection projectionFinderViaReflection, IOptionsMonitor<InceptionHostOptions> hostOptionsMonutor, IOptionsMonitor<TenantsOptions> tenantsOptionsMonitor, IPublisher<ICommand> publisher, IOptionsMonitor<InceptionHostOptions> optionsMonitor, ILogger<ProjectionsStartup> logger)
    {
        this.serviceProvider = serviceProvider;
        this.projectionFinderViaReflection = projectionFinderViaReflection;
        this.publisher = publisher;
        this.hostOptions = hostOptionsMonutor.CurrentValue;
        this.tenants = tenantsOptionsMonitor.CurrentValue;
        this.logger = logger;

        hostOptionsMonutor.OnChange(HostOptionsChanged);
        tenantsOptionsMonitor.OnChange(TenantOptionsChanges);
    }

    public async Task BootstrapAsync()
    {
        await BootstrapInternalAsync(tenants.Tenants, false).ConfigureAwait(false);
    }

    public async Task BootstrapAsync(IEnumerable<string> tenants)
    {
        await BootstrapInternalAsync(tenants, true).ConfigureAwait(false);
    }

    private async Task BootstrapInternalAsync(IEnumerable<string> tenants, bool isNewTenant)
    {
        if (hostOptions.ProjectionsEnabled == false)
            return;

        if (isNewTenant == false)
        {
            List<Task> tenantBootstrapTasks = new List<Task>();
            foreach (var tenant in tenants)
            {
                string scopedTenant = tenant;
                tenantBootstrapTasks.Add(BootstrapProjectionsForTenantAsync(scopedTenant));
            }

            await Task.WhenAll(tenantBootstrapTasks);
        }
        else // experimental
        {
            foreach (var tenant in tenants)
            {
                string scopedTenant = tenant;
                await BootstrapProjectionsForTenantAsync(scopedTenant);
            }
        }
    }

    private async Task BootstrapProjectionsForTenantAsync(string tenant)
    {
        if (hostOptions.ProjectionsEnabled == false)
            return;

        using (var scopedServiceProvider = serviceProvider.CreateScope())
        {
            MessageProcessing.DefaultContextFactory contextFactory = scopedServiceProvider.ServiceProvider.GetRequiredService<One.Inception.MessageProcessing.DefaultContextFactory>();
            MessageProcessing.InceptionContext context = contextFactory.Create(tenant, scopedServiceProvider.ServiceProvider);

            IInitializableProjectionStore storeInitializer = scopedServiceProvider.ServiceProvider.GetRequiredService<IInitializableProjectionStore>();
            LatestProjectionVersionFinder finder = serviceProvider.GetRequiredService<LatestProjectionVersionFinder>();

            foreach (ProjectionVersion viaReflection in finder.GetProjectionVersionsToBootstrap())
            {
                await storeInitializer.InitializeAsync(viaReflection).ConfigureAwait(false);
            }

            await Task.Delay(5000).ConfigureAwait(false); // Enjoying the song => https://www.youtube.com/watch?v=t2nopZVrTH0

            if (hostOptions.SystemServicesEnabled)
            {
                foreach (ProjectionVersion projectionVersion in projectionFinderViaReflection.GetProjectionVersionsToBootstrap())
                {
                    var id = new ProjectionVersionManagerId(projectionVersion.ProjectionName, tenant);
                    var replayOptions = GetReplayOptionsFor(projectionVersion.ProjectionName.GetTypeByContract());

                    var command = new RegisterProjection(id, projectionVersion.Hash, replayOptions);
                    await publisher.PublishAsync(command).ConfigureAwait(false);
                }

                foreach (ProjectionVersion version in projectionFinderViaReflection.GetProjectionVersionsToInitialize())
                {
                    var id = new ProjectionVersionManagerId(version.ProjectionName, tenant);
                    var replayOptions = GetReplayOptionsFor(version.ProjectionName.GetTypeByContract());

                    var command = new InitilizeProjection(id, version.Hash, replayOptions);
                    await publisher.PublishAsync(command).ConfigureAwait(false);
                }
            }
        }
    }

    private void TenantOptionsChanges(TenantsOptions newOptions)
    {
        if (tenants.Tenants.SequenceEqual(newOptions.Tenants) == false) // Check for difference between tenants and newOptions
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("tenant options re-loaded with {@options}", newOptions);

            tenants = newOptions;
        }
    }

    private void HostOptionsChanged(InceptionHostOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("host options re-loaded with {@options}", newOptions);

        hostOptions = newOptions;

    }

    private ReplayEventsOptions GetReplayOptionsFor(Type projectionType)
    {
        DateTimeOffset? afterTimestamp = projectionType.GetProjectionAfterTimestamp();
        if (afterTimestamp.HasValue == false)
            return new ReplayEventsOptions();

        return new ReplayEventsOptions()
        {
            After = afterTimestamp.Value
        };
    }
}
