using One.Inception.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Hosting.Heartbeat;

public class Heartbeat : IHeartbeat
{
    private readonly IPublisher<ISignal> publisher;
    private readonly BoundedContext boundedContext;
    private readonly ILogger<Heartbeat> logger;
    private List<string> configuredTenants;

    private const string TTL = "5000";

    private TenantsOptions tenants;
    private HeartbeatOptions options;

    public Heartbeat(IPublisher<ISignal> publisher, IOptionsMonitor<BoundedContext> boundedContext, IOptionsMonitor<HeartbeatOptions> heartbeatOptions, IOptionsMonitor<TenantsOptions> tenantsOptions, ILogger<Heartbeat> logger)
    {
        this.publisher = publisher;
        this.boundedContext = boundedContext.CurrentValue;
        this.logger = logger;

        tenants = tenantsOptions.CurrentValue;
        configuredTenants = tenants.Tenants.ToList();

        options = heartbeatOptions.CurrentValue;

        heartbeatOptions.OnChange(OnHeartbeatOptionsChanged);
        tenantsOptions.OnChange(OnTenantsOptionsChanged);
    }

    public async Task StartBeatingAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested == false)
        {
            try
            {
                if (options.Enabled)
                {
                    Dictionary<string, string> heartbeatHeaders = new Dictionary<string, string>() { { MessageHeader.TTL, TTL } };
                    var signal = new HeartbeatSignal(boundedContext.Name, configuredTenants);
                    await publisher.PublishAsync(signal, heartbeatHeaders).ConfigureAwait(false);

                }
                await Task.Delay(TimeSpan.FromSeconds(options.IntervalInSeconds), stoppingToken);
            }
            catch (Exception ex) when (ex is TaskCanceledException or ObjectDisposedException)
            {
                // Someone has cancled the task during the delay. In this case we just return without any error.
            }
            catch (Exception ex) when (True(() => logger.LogWarning(ex, "Failed to send heartbeat."))) { }
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Heartbeat has been stopped.");
    }

    private void OnHeartbeatOptionsChanged(HeartbeatOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"{nameof(HeartbeatOptions)} re-loaded with {@options}", newOptions);

        options = newOptions;
    }

    private void OnTenantsOptionsChanged(TenantsOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"{nameof(TenantsOptions)} re-loaded with {@options}", newOptions);

        tenants = newOptions;
        configuredTenants = tenants.Tenants.ToList();
    }
}
