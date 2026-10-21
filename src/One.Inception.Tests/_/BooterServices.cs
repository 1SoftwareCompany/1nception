using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using One.Inception.Multitenancy;

namespace One.Inception;

public static class BooterServices
{
    public static int StartupsCount => new StartupScanner(new DefaulAssemblyScanner()).Scan().Count();

    public static TenantsOptions Tenants(params string[] tenants) => new TenantsOptions { Tenants = tenants };

    public static ServiceCollection With(TenantsOptionsMonitorMock monitor, object startup)
    {
        var scanner = new StartupScanner(new DefaulAssemblyScanner());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptionsMonitor<TenantsOptions>>(monitor);
        services.AddSingleton<ITenantResolver, TenantNameResolver>();
        services.AddSingleton<MessageProcessing.DefaultContextFactory>();
        foreach (Type startupType in scanner.Scan().Concat(scanner.ScanForTenantStartups()))
            services.AddSingleton(startupType, _ => startup);

        return services;
    }

    public static void WaitUntil(Func<bool> condition)
    {
        DateTime timeout = DateTime.UtcNow.AddSeconds(10);
        while (condition() == false && DateTime.UtcNow < timeout)
            Task.Delay(20).GetAwaiter().GetResult();
    }

    class TenantNameResolver : ITenantResolver
    {
        public string Resolve(object source) => source as string;
    }
}
