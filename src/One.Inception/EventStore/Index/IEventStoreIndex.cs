using System.Threading.Tasks;

namespace One.Inception.EventStore.Index;

public interface ISystemEventStoreIndex : IEventStoreIndex, ISystemHandler
{
}

public interface IEventStoreIndex : IMessageHandler
{
    Task IndexAsync(InceptionMessage message);
}
