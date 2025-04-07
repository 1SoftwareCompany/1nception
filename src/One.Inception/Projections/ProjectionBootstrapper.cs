using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using One.Inception.Multitenancy;
using One.Inception.Projections.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace One.Inception.Projections;

internal class ProjectionBootstrapper
{
    private readonly IServiceProvider serviceProvider;
    private readonly ProjectionFinderViaReflection projectionFinderViaReflection;
    private readonly IPublisher<ICommand> publisher;
    private readonly ILogger<ProjectionBootstrapper> logger;
    private InceptionHostOptions hostOptions;
    private TenantsOptions tenants;

    public ProjectionBootstrapper(IServiceProvider serviceProvider, ProjectionFinderViaReflection projectionFinderViaReflection, IOptionsMonitor<InceptionHostOptions> hostOptionsMonutor, IOptionsMonitor<TenantsOptions> tenantsOptionsMonitor, IPublisher<ICommand> publisher, IOptionsMonitor<InceptionHostOptions> optionsMonitor, ILogger<ProjectionBootstrapper> logger)
    {
        this.serviceProvider = serviceProvider;
        this.projectionFinderViaReflection = projectionFinderViaReflection;
        this.publisher = publisher;
        this.hostOptions = hostOptionsMonutor.CurrentValue;
        this.tenants = tenantsOptionsMonitor.CurrentValue;
        this.logger = logger;

        hostOptionsMonutor.OnChange(HostOptionsChanged);

        tenantsOptionsMonitor.OnChange(async newOptions =>
        {
            await OptionsChangedBootstrapProjectionsForTenantAsync(newOptions);
        });
    }

    public async Task BootstrapAsync()
    {
        if (hostOptions.ProjectionsEnabled == false)
            return;

        List<Task> tenantBootstrapTasks = new List<Task>();
        foreach (var tenant in tenants.Tenants)
        {
            string scopedTenant = tenant;
            tenantBootstrapTasks.Add(BootstrapProjectionsForTenantAsync(scopedTenant));
        }

        await Task.WhenAll(tenantBootstrapTasks);
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
                    var command = new RegisterProjection(id, projectionVersion.Hash);
                    publisher.Publish(command);
                }
            }
        }
    }

    private async Task OptionsChangedBootstrapProjectionsForTenantAsync(TenantsOptions newOptions)
    {
        if (tenants.Tenants.SequenceEqual(newOptions.Tenants) == false) // Check for difference between tenants and newOptions
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("host options re-loaded with {@options}", newOptions);

            // Find the difference between the old and new tenants
            // and bootstrap the new tenants
            var newTenants = newOptions.Tenants.Except(tenants.Tenants);
            List<Task> tenantBootstrapTasks = new List<Task>();
            foreach (var tenant in newTenants)
            {
                string scopedTenant = tenant;
                tenantBootstrapTasks.Add(BootstrapProjectionsForTenantAsync(scopedTenant));
            }

            await Task.WhenAll(tenantBootstrapTasks);

            tenants = newOptions;
        }
    }

    private void HostOptionsChanged(InceptionHostOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("host options re-loaded with {@options}", newOptions);

        hostOptions = newOptions;

    }
}
