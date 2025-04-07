using System.Collections.Generic;
using One.Inception.AtomicAction;
using One.Inception.EventStore;
using One.Inception.EventStore.InMemory;
using One.Inception.EventStore.Integrity;
using One.Inception.IntegrityValidation;
using One.Inception.Tests.TestModel;
using Machine.Specifications;

namespace One.Inception.Tests.InMemoryEventStoreSuite;

[Subject("AggregateRoot")]
public class When_saving_aggregate_root_in_event_store
{
    Establish context = () =>
    {
        versionService = new InMemoryAggregateRootAtomicAction();
        eventStoreStorage = new InMemoryEventStoreStorage();
        eventStore = new InMemoryEventStore(eventStoreStorage);
        eventStorePlayer = new InMemoryEventStorePlayer(eventStoreStorage);
        integrityPpolicy = new EventStreamIntegrityPolicy(null);
        eventStoreFactory = new EventStoreFactory(eventStore, null);
        aggregateRepository = new AggregateRepository(eventStoreFactory, versionService, integrityPpolicy, new AggregateCommitInterceptor(new List<EmptyAggregateTransformer>()), null);
        id = new TestAggregateId();
        aggregateRoot = new TestAggregateRoot(id);
    };

    Because of = async () => await aggregateRepository.SaveAsync(aggregateRoot).ConfigureAwait(false);

    It should_instansiate_aggregate_root = async () => (await aggregateRepository.LoadAsync<TestAggregateRoot>(id).ConfigureAwait(false)).ShouldNotBeNull();

    It should_instansiate_aggregate_root_with_valid_state = async () => (await aggregateRepository.LoadAsync<TestAggregateRoot>(id).ConfigureAwait(false)).Data.State.Id.ShouldEqual(id);

    static TestAggregateId id;
    static InMemoryEventStoreStorage eventStoreStorage;
    static IAggregateRootAtomicAction versionService;
    static IEventStore eventStore;
    static IEventStorePlayer eventStorePlayer;
    static IAggregateRepository aggregateRepository;
    static TestAggregateRoot aggregateRoot;
    static IIntegrityPolicy<EventStream> integrityPpolicy;
    static EventStoreFactory eventStoreFactory;
}
