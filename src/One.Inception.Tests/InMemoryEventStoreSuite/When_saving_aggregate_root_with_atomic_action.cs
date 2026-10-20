using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.Options;
using One.Inception.AtomicAction;
using One.Inception.EventStore;
using One.Inception.EventStore.InMemory;
using One.Inception.EventStore.Integrity;
using One.Inception.Tests.TestModel;
using Result = One.Inception.Userfull.Result;

namespace One.Inception.Tests.InMemoryEventStoreSuite;

[Subject("AggregateRoot")]
public class When_saving_aggregate_root_with_atomic_action
{
    Establish context = () =>
    {
        atomicAction = new RecordingAggregateRootAtomicAction();
        var eventStoreFactory = new EventStoreFactory(new InMemoryEventStore(new InMemoryEventStoreStorage()), null);
        var boundedContext = Options.Create(new BoundedContext { Name = "volumes" });
        aggregateRepository = new AggregateRepository(eventStoreFactory, atomicAction, new EventStreamIntegrityPolicy(null), new AggregateCommitInterceptor(new List<EmptyAggregateTransformer>()), boundedContext, null);
        id = new TestAggregateId();
    };

    Because of = async () => await aggregateRepository.SaveAsync(new TestAggregateRoot(id)).ConfigureAwait(false);

    It should_lock_the_aggregate_root_within_the_bounded_context = () => atomicAction.Resource.Value.ShouldEqual($"volumes:{id.Value}");

    static TestAggregateId id;
    static RecordingAggregateRootAtomicAction atomicAction;
    static IAggregateRepository aggregateRepository;
}

class RecordingAggregateRootAtomicAction : IAggregateRootAtomicAction
{
    public AtomicActionResource Resource { get; private set; }

    public async Task<One.Inception.Userfull.Result<bool>> ExecuteAsync(AtomicActionResource resource, int aggregateRootRevision, Func<Task> action)
    {
        Resource = resource;
        await action().ConfigureAwait(false);
        return Result.Success;
    }

    public void Dispose() { }
}
