using One.Inception.Migration.Middleware.Tests.TestModel.Bar;
using One.Inception.Migration.Middleware.Tests.TestModel.Foo;
using One.Inception.Migration.Middleware.Tests.TestModel.FooBar;
using One.Inception.EventStore;
using Machine.Specifications;
using System.Collections.Generic;
using System.Linq;
using One.Inception.Migrations.TestMigration;
using System;

namespace One.Inception.Migrations;

[Subject("Migration")]
public class When_producing_new_aggregate_from_two
{
    Establish context = () =>
    {
        migration = new ProduceNewAggregateMigration();
        migrationOuput = new List<AggregateCommit>();
        var fooId = new FooId("1234", "testtenant");
        aggregateCommitFoo = new AggregateCommit(fooId.RawId, 1, new List<IEvent>
            {
                new TestCreateEventFoo(fooId),
                new TestUpdateEventFoo(fooId, string.Empty)
            }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());

        var barId = new BarId("4321", "testtenant");
        aggregateCommitBar = new AggregateCommit(barId.RawId, 1, new List<IEvent> { new TestCreateEventBar(barId) }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
    };

    Because of = () =>
    {
        migrationOuput.AddRange(migration.Apply(aggregateCommitFoo).ToList());
        migrationOuput.AddRange(migration.Apply(aggregateCommitBar).ToList());
    };

    It the_migration_should_return_two_aggegateCommits = () => migrationOuput.Count.ShouldEqual(2);
    It the_migration_should_contain_correnct_number_of_events = () => migrationOuput.SelectMany(x => x.Events).Count().ShouldEqual(3);
    It the_migration_should_contain_only_events_from_new_aggregate =
        () => migrationOuput.Select(x => x.Events.Select(e => e.GetType().GetContractId())).ShouldContain(contracts);

    static IMigration<AggregateCommit, IEnumerable<AggregateCommit>> migration;
    static AggregateCommit aggregateCommitFoo;
    static AggregateCommit aggregateCommitBar;
    static List<AggregateCommit> migrationOuput;

    static List<string> contracts = new List<string>
    {
        typeof(TestCreateEventFooBar).GetContractId(),
        typeof(TestUpdateEventFooBar).GetContractId()
    };
}
