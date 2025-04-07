using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace One.Inception.DangerZone;

public class DangerZoneOptions
{
    public DangerZoneOptions()
    {
        ProtectedTenants = new List<string>();
    }

    public List<string> ProtectedTenants { get; set; }
}

public class DangerZoneOptionsProvider : InceptionOptionsProviderBase<DangerZoneOptions>
{
    public DangerZoneOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(DangerZoneOptions options)
    {
        configuration.GetSection("Inception:DangerZone").Bind(options);
    }
}
