﻿using System;
using System.Collections.Generic;
using System.Text;
using One.Inception.EventStore;
using One.Inception.Migration.Middleware.Tests.TestModel.Bar;
using One.Inception.Migration.Middleware.Tests.TestModel.Foo;
using One.Inception.Migration.Middleware.Tests.TestModel.FooBar;

namespace One.Inception.Migrations.TestMigration;

public class SplitAggregateMigration : IMigration<AggregateCommit, IEnumerable<AggregateCommit>>
{
    readonly string targetAggregateName = "FooBar".ToLowerInvariant();
    static readonly FooBarId id = new FooBarId("1234", "testtenant");

    public IEnumerable<AggregateCommit> Apply(AggregateCommit current)
    {
        if (ShouldApply(current))
        {
            var fooId = new FooId("1234", "testtenant");
            var newFooEvents = new List<IEvent>();
            foreach (IEvent @event in current.Events)
            {
                if (@event.GetType() == typeof(TestCreateEventFooBar))
                {
                    newFooEvents.Add(new TestCreateEventFoo(fooId));
                }
                else if (@event.GetType() == typeof(TestUpdateEventFooBar))
                {
                    var theEvent = @event as TestUpdateEventFooBar;
                    newFooEvents.Add(new TestUpdateEventFoo(fooId, theEvent.UpdatedFieldValue));
                }
            }
            var aggregateCommitFoo = new AggregateCommit(fooId.RawId, current.Revision, newFooEvents, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
            yield return aggregateCommitFoo;

            var barId = new BarId("5432", "testtenant");
            var newBarEvents = new List<IEvent>();
            foreach (IEvent @event in current.Events)
            {
                if (@event.GetType() == typeof(TestCreateEventFooBar))
                {
                    newBarEvents.Add(new TestCreateEventBar(barId));
                }
                else if (@event.GetType() == typeof(TestUpdateEventFooBar))
                {
                    var theEvent = @event as TestUpdateEventFooBar;
                    newBarEvents.Add(new TestUpdateEventBar(barId, theEvent.UpdatedFieldValue));
                }
            }
            var aggregateCommitBar = new AggregateCommit(barId.RawId, current.Revision, newFooEvents, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());

            yield return aggregateCommitBar;

        }
        else
            yield return current;
    }

    public bool ShouldApply(AggregateCommit current)
    {
        var urnRaw = new Urn(Encoding.UTF8.GetString(current.AggregateRootId.Span));
        var urn = AggregateRootId.Parse(urnRaw.Value);
        string currentAggregateName = urn.AggregateRootName;

        if (currentAggregateName == targetAggregateName)
            return true;

        return false;
    }
}
