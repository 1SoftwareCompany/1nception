using System;
using System.Collections.Generic;
using System.Linq;
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
    private TenantsOptions tenantOptions;
    private readonly ILogger<Booter> logger;

    public Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger)
    {
        this.serviceProvider = serviceProvider;
        this.tenantOptions = monitor.CurrentValue;
        this.logger = logger;

        monitor.OnChange(async (newOptions) => await OnTenantsOptionsChanged(newOptions).ConfigureAwait(false));
    }

    public async Task BootstrapInceptionAsync()
    {
        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        IEnumerable<Type> startups = scanner.Scan();

        foreach (var startupType in startups)
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
        if (tenantOptions.Tenants.SequenceEqual(newOptions.Tenants) == false) // Check for difference between tenants and newOptions
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("tenant options re-loaded with {@options}", newOptions);

            // Find the difference between the old and new tenants
            // and bootstrap the new tenants
            var newTenants = newOptions.Tenants.Except(tenantOptions.Tenants);
            await BootstrapNewlyStartedTenantsAsync(newTenants).ConfigureAwait(false);

            tenantOptions = newOptions;
        }
    }

    private async Task BootstrapNewlyStartedTenantsAsync(IEnumerable<string> newTenants)
    {
        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        IEnumerable<Type> startups = scanner.Scan();

        foreach (var startupType in startups)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            await startup.BootstrapAsync(newTenants).ConfigureAwait(false);
        }

        IEnumerable<Type> tenantStartups = scanner.ScanForTenantStartups();
        foreach (string tenant in newTenants)
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
}
