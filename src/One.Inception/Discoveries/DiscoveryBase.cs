using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace One.Inception.Discoveries;

public abstract class DiscoveryBase<TInceptionService> : IDiscovery<TInceptionService>    //where TInceptionService : IInceptionService
{
    public virtual string Name { get { return this.GetType().Name; } }

    public IDiscoveryResult<TInceptionService> Discover(DiscoveryContext context)
    {
        return DiscoverFromAssemblies(context);
    }

    protected abstract DiscoveryResult<TInceptionService> DiscoverFromAssemblies(DiscoveryContext context);

    protected IEnumerable<DiscoveredModel> DiscoverModel<TService, TImplementation>(ServiceLifetime lifestyle, bool replaceExistingService = false)
    {
        yield return new DiscoveredModel(typeof(TService), typeof(TImplementation), lifestyle) { CanOverrideDefaults = replaceExistingService };

        if (typeof(TService) != typeof(TImplementation))
            yield return new DiscoveredModel(typeof(TImplementation), typeof(TImplementation), lifestyle);
    }
}
