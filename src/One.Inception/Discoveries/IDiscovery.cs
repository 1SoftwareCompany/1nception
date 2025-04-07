namespace One.Inception.Discoveries;

public interface IDiscovery<out TInceptionService>    //where TInceptionService : IInceptionService
{
    string Name { get; }

    IDiscoveryResult<TInceptionService> Discover(DiscoveryContext context);
}
