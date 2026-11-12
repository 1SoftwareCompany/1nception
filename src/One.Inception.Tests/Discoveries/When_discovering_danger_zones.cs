using System.Linq;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using One.Inception.DangerZone;

namespace One.Inception.Discoveries;

[Subject("Discoveries")]
public class When_discovering_danger_zones
{
    Establish context = () =>
    {
        services = new ServiceCollection();
        discoveryContext = new DiscoveryContext(new[] { typeof(When_discovering_danger_zones).Assembly }, new ConfigurationMock());
    };

    Because of = () => new InceptionServicesProvider(services, discoveryContext.Configuration)
        .HandleDiscoveredModel(new DangerZoneDiscovery().Discover(discoveryContext));

    It should_register_all_concrete_implementations = () => services.Select(x => x.ImplementationType)
        .OrderBy(x => x.Name).SequenceEqual(new[] { typeof(FirstDangerZone), typeof(SecondDangerZone) }).ShouldBeTrue();

    It should_register_implementations_as_singletons = () => services.All(x =>
        x.ServiceType == typeof(IDangerZone) && x.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();

    It should_resolve_all_implementations = () =>
    {
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IDangerZone>().Count().ShouldEqual(2);
        provider.GetServices<IDangerZone>().First().ShouldBeTheSameAs(provider.GetServices<IDangerZone>().First());
    };

    static IServiceCollection services;
    static DiscoveryContext discoveryContext;
}

public class FirstDangerZone : IDangerZone
{
    public Task WipeDataAsync(string tenant) => Task.CompletedTask;
}

public class SecondDangerZone : IDangerZone
{
    public Task WipeDataAsync(string tenant) => Task.CompletedTask;
}

public abstract class AbstractDangerZone : IDangerZone
{
    public abstract Task WipeDataAsync(string tenant);
}

public class GenericDangerZone<T> : IDangerZone
{
    public Task WipeDataAsync(string tenant) => Task.CompletedTask;
}
