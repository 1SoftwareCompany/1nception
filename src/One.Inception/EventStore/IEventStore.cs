using One.Inception.EventStore.Index;
using System.Threading.Tasks;

namespace One.Inception.EventStore;

public interface IEventStore
{
    Task AppendAsync(AggregateCommit aggregateCommit);
    Task AppendAsync(AggregateEventRaw eventRaw);
    Task<EventStream> LoadAsync(IBlobId aggregateId);
    Task<bool> DeleteAsync(AggregateEventRaw eventRaw);
    Task<LoadAggregateRawEventsWithPagingResult> LoadWithPagingAsync(IBlobId aggregateId, PagingOptions pagingOptions);
    Task<AggregateEventRaw> LoadAggregateEventRaw(IndexRecord indexRecord);
}

public interface IEventStore<TSettings> : IEventStore
    where TSettings : class
{
}
