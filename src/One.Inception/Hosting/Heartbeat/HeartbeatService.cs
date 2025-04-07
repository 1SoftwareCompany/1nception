using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace One.Inception.Hosting.Heartbeat;

public sealed class HeartbeatService : BackgroundService
{
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(IServiceProvider services, ILogger<HeartbeatService> logger)
    {
        Services = services;
        _logger = logger;
    }

    public IServiceProvider Services { get; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consume Scoped Service Hosted Service running.");

        return DoWork(stoppingToken);
    }

    private Task DoWork(CancellationToken stoppingToken)
    {
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("HeartbeatService is working.");

            var heartbeat = Services.GetRequiredService<IHeartbeat>();
            return heartbeat.StartBeatingAsync(stoppingToken);
        }
        catch (Exception ex) when (True(() => _logger.LogError("Failed to send heartbeat.")))
        {
            return Task.FromException(ex);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consume Scoped Service Hosted Service is stopping.");

        await base.StopAsync(stoppingToken);
    }
}
