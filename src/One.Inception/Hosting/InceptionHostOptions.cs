using Microsoft.Extensions.Configuration;
using System;

namespace One.Inception;

public class InceptionHostOptions
{
    private TimeSpan shutdownTimeout = TimeSpan.FromSeconds(15);

    public TimeSpan ShutdownTimeout
    {
        get => shutdownTimeout;
        set
        {
            if (value <= TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue - 1)
                throw new ArgumentOutOfRangeException(nameof(value), "ShutdownTimeout must be a positive, finite timer interval.");
            shutdownTimeout = value;
        }
    }

    public bool ApplicationServicesEnabled { get; set; } = true;
    public bool ProjectionsEnabled { get; set; } = true;
    public bool PortsEnabled { get; set; } = true;
    public bool ProcessManagersEnabled { get; set; } = true;
    public bool GatewaysEnabled { get; set; } = true;
    public bool TriggersEnabled { get; set; } = true;
    public bool MigrationsEnabled { get; set; } = false;
    public bool SystemServicesEnabled { get; set; } = true;
    public bool RpcApiEnabled { get; set; } = false;
    public bool NodeBroadcastEnabled { get; set; } = true;
    public bool AutoUpdatesEnabled { get; set; } = true;
}

public class InceptionHostOptionsProvider : InceptionOptionsProviderBase<InceptionHostOptions>
{
    public InceptionHostOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(InceptionHostOptions options)
    {
        configuration.GetSection("Inception").Bind(options);
    }
}
