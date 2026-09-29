using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace One.Inception.Hosting.Heartbeat;

public class HeartbeatOptions
{
    public bool Enabled { get; set; } = false;

    [Range(5, 3600, ErrorMessage = "The configuration `Inception:Heartbeat:IntervalInSeconds` cannot be negative as it represents a time interval in seconds.")]
    public uint IntervalInSeconds { get; set; } = 5;
}

public class HeartbeatOptionsProvider : InceptionOptionsProviderBase<HeartbeatOptions>
{
    public HeartbeatOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(HeartbeatOptions options)
    {
        configuration.GetSection("Inception:Heartbeat").Bind(options);
    }
}
