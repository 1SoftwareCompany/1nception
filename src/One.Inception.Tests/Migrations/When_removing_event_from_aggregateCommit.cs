using One.Inception.Migration.Middleware.Tests.TestModel.Foo;
using One.Inception.EventStore;
using Machine.Specifications;
using System.Collections.Generic;
using System.Linq;
using One.Inception.Migrations.TestMigration;
using System;

namespace One.Inception.Migrations;

[Subject("Migration")]
public class When_removing_event_from_aggregateCommit
{
    Establish context = () =>
    {
        migration = new RemoveEventMigration();
        var id = new FooId("1234", "testtenant");
        aggregateCommitFoo = new AggregateCommit(id.RawId, 1, new List<IEvent>
            {
                new TestCreateEventFoo(id),
                new TestUpdateEventFoo(id, string.Empty)
            }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
    };

    Because of = () => migrationOuput = migration.Apply(aggregateCommitFoo).ToList();

    It the_migration_should_add_new_event = () => migrationOuput.Single().Events.Count.ShouldEqual(1);

    static IMigration<AggregateCommit, IEnumerable<AggregateCommit>> migration;
    static AggregateCommit aggregateCommitFoo;
    static IList<AggregateCommit> migrationOuput;
}
