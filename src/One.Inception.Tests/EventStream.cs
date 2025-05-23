﻿using System;
using System.Collections.Generic;
using One.Inception.EventStore;
using One.Inception.Tests.TestModel;
using Machine.Specifications;

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
