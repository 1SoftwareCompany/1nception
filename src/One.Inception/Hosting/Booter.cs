using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    private static readonly TimeSpan WaitWarningInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] DefaultBootstrapRetryDelays = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)];
    private readonly SemaphoreSlim tenantsChangeLock = new SemaphoreSlim(1, 1);
    private readonly TimeSpan[] bootstrapRetryDelays;

    public Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger)
        : this(serviceProvider, monitor, logger, DefaultBootstrapRetryDelays) { }

    internal Booter(IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> monitor, ILogger<Booter> logger, TimeSpan[] bootstrapRetryDelays)
    {
        this.serviceProvider = serviceProvider;
        this.monitor = monitor;
        this.tenantOptions = monitor.CurrentValue;
        this.logger = logger;
        this.bootstrapRetryDelays = bootstrapRetryDelays;

        monitor.OnChange(async _ => await OnTenantsOptionsChanged().ConfigureAwait(false));
    }

    public async Task BootstrapInceptionAsync()
    {
        await tenantsChangeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            tenantOptions = monitor.CurrentValue;
            await BootstrapAllTenantsAsync().ConfigureAwait(false);
        }
        finally
        {
            tenantsChangeLock.Release();
        }
    }

    private async Task BootstrapAllTenantsAsync()
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

    private async Task OnTenantsOptionsChanged()
    {
        while (await tenantsChangeLock.WaitAsync(WaitWarningInterval).ConfigureAwait(false) == false)
        {
            logger.LogWarning("Still waiting for a previous tenant bootstrap to finish before applying the tenants change");
        }

        Exception bootstrapFailure = null;
        try
        {
            TenantsOptions newOptions = monitor.CurrentValue;
            if (tenantOptions.Tenants.SequenceEqual(newOptions.Tenants) == false)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("tenant options re-loaded with {@options}", newOptions);

                var newTenants = newOptions.Tenants.Except(tenantOptions.Tenants).ToList();
                bootstrapFailure = await TryBootstrapNewlyStartedTenantsAsync(newTenants).ConfigureAwait(false);

                if (bootstrapFailure is null)
                    tenantOptions = newOptions;
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
            try
            {
                await BootstrapNewlyStartedTenantsAsync(newTenants).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex) when (attempt < bootstrapRetryDelays.Length)
            {
                logger.LogWarning(ex, "Failed to bootstrap new tenants {@tenants}. Retrying in {delay}", newTenants, bootstrapRetryDelays[attempt]);
                await Task.Delay(bootstrapRetryDelays[attempt]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    private void RestartApplication(Exception bootstrapFailure)
    {
        logger.LogCritical(bootstrapFailure, "Failed to bootstrap new tenants after {attempts} attempts. Stopping the application so it restarts and bootstraps all tenants", bootstrapRetryDelays.Length + 1);

        IHostApplicationLifetime lifetime = serviceProvider.GetService<IHostApplicationLifetime>();
        if (lifetime is null)
            ExceptionDispatchInfo.Throw(bootstrapFailure);

        lifetime.StopApplication();
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
