using One.Inception.Projections.Cassandra.EventSourcing;
using Microsoft.Extensions.Options;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace One.Inception.EventStore.Index;

[DataContract(Name = "3d59f948-870f-4b12-ada6-9603627aaab6")]
public class EventToAggregateRootId : ISystemEventStoreIndex
{
    private readonly IIndexStore indexStore;
    private readonly IOptions<BoundedContext> bcOptions;

    public EventToAggregateRootId() { }

    public EventToAggregateRootId(IIndexStore indexStore, IOptions<BoundedContext> bcOptions)
    {
        this.indexStore = indexStore;
        this.bcOptions = bcOptions;
    }

    public Task IndexAsync(InceptionMessage message)
    {
        if (message.Payload is IEvent @event)
        {
            string eventTypeId = @event.Unwrap().GetType().GetContractId();
            return ExecuteIndexAsync(eventTypeId, message);
        }
        else if (message.Payload is IPublicEvent @publicEvent && IsPublicEventFromCurrentBoundedContext(message))
        {
            string eventTypeId = @publicEvent.GetType().GetContractId();
            return ExecuteIndexAsync(eventTypeId, message);
        }

        return Task.CompletedTask;
    }

    private Task ExecuteIndexAsync(string eventTypeId, InceptionMessage message)
    {
        var record = new IndexRecord(eventTypeId, Encoding.UTF8.GetBytes(message.GetRootId()), message.GetRevision(), message.GetRootEventPosition(), message.GetTimestamp());

        return indexStore.ApendAsync(record);
    }

    /// <summary>
    /// Public events should be only indexed when they are within their own BC.
    /// </summary>
    private bool IsPublicEventFromCurrentBoundedContext(InceptionMessage message)
    {
        return bcOptions.Value.Name.Equals(message.BoundedContext, System.StringComparison.OrdinalIgnoreCase);
    }
}
