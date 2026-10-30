using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.MessageProcessing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception.DangerZone;

public sealed class DangerZoneExecutor
{
    private DangerZoneOptions options;
    private readonly IEnumerable<IDangerZone> dangerZones;
    private readonly ILogger<DangerZoneExecutor> logger;
    private readonly IInceptionContextAccessor contextAccessor;

    public DangerZoneExecutor(IInceptionContextAccessor contextAccessor, IOptionsMonitor<DangerZoneOptions> monitor, IEnumerable<IDangerZone> dangerZones, ILogger<DangerZoneExecutor> logger)
    {
        this.contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        options = monitor.CurrentValue;
        this.dangerZones = dangerZones;
        this.logger = logger;

        monitor.OnChange((newOptions) =>
        {
            options = newOptions;
        });
    }

    public async Task WipeDataAsync(string tenant)
    {
        if (options.ProtectedTenants.Contains(tenant))
        {
            logger.LogInformation("Tenant {tenant} is protected and cannot be wiped.", tenant);
            return;
        }

        if (tenant.Equals(contextAccessor.Context.Tenant, StringComparison.Ordinal) == false)
        {
            logger.LogError("Tenant mismatch. The tenant to be wiped is different from the current tenant.");
            return;
        }

        logger.LogInformation("Wiping data for tenant {tenant}", tenant);

        foreach (var item in dangerZones)
        {
            await item.WipeDataAsync(tenant).ConfigureAwait(false);
        }

        logger.LogInformation("Data for tenant {tenant} has been wiped.", tenant);
    }
}
