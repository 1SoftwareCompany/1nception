using One.Inception.MessageProcessing;
using One.Inception.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

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

        monitor.OnChange(OnTenantsOptionsChanged);
    }

    public void BootstrapInception()
    {
        var scanner = new StartupScanner(new DefaulAssemblyScanner());
        IEnumerable<Type> startups = scanner.Scan();

        foreach (var startupType in startups)
        {
            IInceptionStartup startup = (IInceptionStartup)serviceProvider.GetRequiredService(startupType);
            startup.Bootstrap();
        }

        IEnumerable<Type> tenantStartups = scanner.ScanForTenantStartups();
        foreach (var tenantStartupType in tenantStartups)
        {
            foreach (string tenant in tenantOptions.Tenants)
            {
                using (var scopedServiceProvider = serviceProvider.CreateScope())
                {
                    DefaultContextFactory contextFactory = scopedServiceProvider.ServiceProvider.GetRequiredService<DefaultContextFactory>();
                    InceptionContext context = contextFactory.Create(tenant, scopedServiceProvider.ServiceProvider);

                    ITenantStartup tenantStartUp = (ITenantStartup)context.ServiceProvider.GetRequiredService(tenantStartupType);
                    tenantStartUp.Bootstrap();
                }
            }
        }
    }

    private void OnTenantsOptionsChanged(TenantsOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("tenants options re-loaded with {@options}", newOptions);

        tenantOptions = newOptions;
    }
}
