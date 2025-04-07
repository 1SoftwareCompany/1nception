namespace One.Inception.Discoveries;

public interface IInceptionServicesProvider
{
    void HandleDiscoveredModel(IDiscoveryResult<object> discoveryResult);
}
