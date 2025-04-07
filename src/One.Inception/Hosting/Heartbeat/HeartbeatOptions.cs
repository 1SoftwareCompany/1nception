using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace One.Inception.Hosting.Heartbeat;

public class HeartbeatOptions
{
    [Range(5, 3600, ErrorMessage = "The configuration `Inception:Heartbeat:IntervalInSeconds` cannot be negative as it represents a time interval in seconds.")]
    public uint IntervalInSeconds { get; set; } = 5;
}

public class HeartbeaOptionsProvider : InceptionOptionsProviderBase<HeartbeatOptions>
{
    public HeartbeaOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(HeartbeatOptions options)
    {
        configuration.GetSection("Inception:Heartbeat").Bind(options);
    }
}
