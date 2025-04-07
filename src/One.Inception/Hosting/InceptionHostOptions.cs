using Microsoft.Extensions.Configuration;

namespace One.Inception;

public class InceptionHostOptions
{
    public bool ApplicationServicesEnabled { get; set; } = true;
    public bool ProjectionsEnabled { get; set; } = true;
    public bool PortsEnabled { get; set; } = true;
    public bool SagasEnabled { get; set; } = true;
    public bool GatewaysEnabled { get; set; } = true;
    public bool TriggersEnabled { get; set; } = true;
    public bool MigrationsEnabled { get; set; } = false;
    public bool SystemServicesEnabled { get; set; } = true;
    public bool RpcApiEnabled { get; set; } = false;
}

public class InceptionHostOptionsProvider : InceptionOptionsProviderBase<InceptionHostOptions>
{
    public InceptionHostOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(InceptionHostOptions options)
    {
        configuration.GetSection("Inception").Bind(options);
    }
}
