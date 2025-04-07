using Microsoft.Extensions.Logging;

namespace One.Inception.EventStore;

public interface IEventStoreFactory
{
    IEventStore GetEventStore();
}

public sealed class EventStoreFactory : IEventStoreFactory
{
    private readonly IEventStore eventStore;
    private readonly ILogger<InceptionEventStore> logger;

    public EventStoreFactory(IEventStore eventStore, ILogger<InceptionEventStore> logger)
    {
        this.eventStore = eventStore;
        this.logger = logger;
    }

    public IEventStore GetEventStore() => new InceptionEventStore(eventStore, logger);
}
