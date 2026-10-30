using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using One.Inception.DangerZone;

namespace One.Inception.Discoveries;

public class DangerZoneDiscovery : DiscoveryBase<IDangerZone>
{
    protected override DiscoveryResult<IDangerZone> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<IDangerZone>(DiscoverDangerZones(context));
    }

    private static IEnumerable<DiscoveredModel> DiscoverDangerZones(DiscoveryContext context)
    {
        foreach (var type in context.FindService<IDangerZone>())
        {
            if (type.IsClass && type.ContainsGenericParameters == false)
                yield return new DiscoveredModel(typeof(IDangerZone), type, ServiceLifetime.Singleton) { CanAddMultiple = true };
        }
    }
}
