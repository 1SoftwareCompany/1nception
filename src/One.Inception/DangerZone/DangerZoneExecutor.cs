﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace One.Inception.DangerZone;

public sealed class DangerZoneExecutor
{
    private DangerZoneOptions options;
    private readonly IEnumerable<IDangerZone> dangerZones;
    private readonly ILogger<DangerZoneExecutor> logger;

    public DangerZoneExecutor(IOptionsMonitor<DangerZoneOptions> monitor, IEnumerable<IDangerZone> dangerZones, ILogger<DangerZoneExecutor> logger)
    {
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

        logger.LogInformation("Wiping data for tenant {tenant}", tenant);

        foreach (var item in dangerZones)
        {
            await item.WipeDataAsync(tenant).ConfigureAwait(false);
        }

        logger.LogInformation("Data for tenant {tenant} has been wiped.", tenant);
    }
}
