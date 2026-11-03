using System;
using System.Collections.Generic;
using System.Threading;
using One.Inception.EventStore;
using One.Inception.EventStore.InMemory;
using One.Inception.EventStore.Integrity;
using One.Inception.MessageProcessing;
using One.Inception.Tests.InMemoryEventStoreSuite;
using One.Inception.Tests.TestModel;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace One.Inception.Tests;

[Subject("AggregateRoot")]
public class When_build_aggregate_root_from_events
{
    Establish context = () =>
    {
        id = new TestAggregateId();

        var commits = new List<AggregateCommit>();
        commits.Add(new AggregateCommit(id.RawId, 1, new List<IEvent>() { new TestCreateEvent(id) }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime()));
        commits.Add(new AggregateCommit(id.RawId, 2, new List<IEvent>() { new TestUpdateEvent(id, "When_build_aggregate_root_from_events") }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime()));

        eventStream = new EventStream(commits);
    };

    Because of = () => eventStream.TryRestoreFromHistory<TestAggregateRoot>(out ar);

    It should_instansiate_aggregate_root = () => ar.ShouldNotBeNull();
    It should_instansiate_aggregate_root_with_valid_state = () => ar.State.Id.ShouldEqual(id);

    static TestAggregateId id;
    static EventStream eventStream;
    static TestAggregateRoot ar;
}

[Subject("AggregateRoot")]
public class When_replaying_aggregate_root_is_canceled_between_events
{
    Establish context = () =>
    {
        id = new TestAggregateId();
        CancelingAggregateRootState.Cancellation = new CancellationTokenSource();
        CancelingAggregateRootState.ReplayedEvents = 0;

        var commits = new List<AggregateCommit>();
        commits.Add(new AggregateCommit(id.RawId, 1, new List<IEvent>() { new TestCreateEvent(id) }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime()));
        commits.Add(new AggregateCommit(id.RawId, 2, new List<IEvent>() { new TestUpdateEvent(id, "must_not_be_replayed") }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime()));
        commits.Add(new AggregateCommit(id.RawId, 3, new List<IEvent>() { new TestUpdateEvent(id, "must_not_be_replayed") }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime()));
        eventStream = new EventStream(commits);
    };

    Because of = () => expectedException = Catch.Exception(() => eventStream.TryRestoreFromHistory<CancelingAggregateRoot>(out ar, CancelingAggregateRootState.Cancellation.Token));

    It should_stop_with_operation_canceled = () => expectedException.ShouldBeAssignableTo<OperationCanceledException>();
    It should_not_replay_the_events_after_the_cancellation = () => CancelingAggregateRootState.ReplayedEvents.ShouldEqual(1);

    static TestAggregateId id;
    static EventStream eventStream;
    static Exception expectedException;
    static CancelingAggregateRoot ar;
}

[Subject("AggregateRoot")]
public class When_loading_aggregate_root_and_the_context_token_is_canceled
{
    Establish context = async () =>
    {
        id = new TestAggregateId();
        var contextAccessor = new CanceledContextAccessor();
        var eventStoreFactory = new EventStoreFactory(new InMemoryEventStore(new InMemoryEventStoreStorage()), null);
        repository = new AggregateRepository(eventStoreFactory, new InMemoryAggregateRootAtomicAction(), new EventStreamIntegrityPolicy(null), new AggregateCommitInterceptor(new List<EmptyAggregateTransformer>()), Options.Create(new BoundedContext { Name = "tests" }), null, contextAccessor);
        await repository.SaveAsync(new TestAggregateRoot(id));
    };

    Because of = () => expectedException = Catch.Exception(() => repository.LoadAsync<TestAggregateRoot>(id).GetAwaiter().GetResult());

    It should_stop_with_operation_canceled = () => expectedException.ShouldBeAssignableTo<OperationCanceledException>();

    static TestAggregateId id;
    static AggregateRepository repository;
    static Exception expectedException;

    class CanceledContextAccessor : IInceptionContextAccessor
    {
        public InceptionContext Context { get; set; } = new InceptionContext("tests", new ServiceCollection().BuildServiceProvider()) { CancellationToken = new CancellationToken(true) };
    }
}

public class CancelingAggregateRoot : AggregateRoot<CancelingAggregateRootState>
{
    CancelingAggregateRoot() { }
}

public class CancelingAggregateRootState : AggregateRootState<CancelingAggregateRoot, TestAggregateId>
{
    public static CancellationTokenSource Cancellation;
    public static int ReplayedEvents;

    public override TestAggregateId Id { get; set; }

    public void When(TestCreateEvent e)
    {
        Id = e.Id;
        ReplayedEvents++;
        Cancellation.Cancel();
    }

    public void When(TestUpdateEvent e) => ReplayedEvents++;
}

[Subject("AggregateRoot")]
public class When_build_aggregate_root_from_history_without_the_initial_event
{
    Establish context = () =>
    {
        id = new TestAggregateId();
        var commits = new List<AggregateCommit>();
        commits.Add(new AggregateCommit(id.RawId, 2, new List<IEvent>() { new TestUpdateEvent(id, "When_build_aggregate_root_from_history_without_the_initial_event") }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime()));
        eventStream = new EventStream(commits);
    };

    Because of = () => expectedException = Catch.Exception(() => eventStream.TryRestoreFromHistory<TestAggregateRoot>(out ar));

    It an__AggregateRootException__should_be_thrown = () => expectedException.ShouldBeOfExactType<AggregateRootException>();

    static TestAggregateId id;
    static EventStream eventStream;
    static Exception expectedException;
    static TestAggregateRoot ar;
}
