using Microsoft.Extensions.DependencyInjection;
using One.Inception.Serializer;
using System.Collections.Generic;
using One.Inception.Discoveries;

namespace One.Inception.EventStore;

public class JsonSerializerDiscovery : DiscoveryBase<ISerializer>
{
    protected override DiscoveryResult<ISerializer> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<ISerializer>(GetModels(context));
    }

    IEnumerable<DiscoveredModel> GetModels(DiscoveryContext context)
    {
        yield return new DiscoveredModel(typeof(ISerializer), typeof(MissingSerializer), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(IEventLookUp), typeof(MissingEventLookUp), ServiceLifetime.Singleton);
    }
}
