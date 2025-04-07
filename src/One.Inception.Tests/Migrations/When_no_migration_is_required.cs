using One.Inception.Migration.Middleware.Tests.TestModel.Bar;
using One.Inception.EventStore;
using Machine.Specifications;
using System.Collections.Generic;
using System.Linq;
using One.Inception.Migrations.TestMigration;
using System;

namespace One.Inception.Migrations;

[Subject("Migration")]
public class When_no_migration_is_required
{
    Establish context = () =>
    {
        var barId = new BarId("1234", "testtenant");
        migration = new SimpleMigration();
        aggregateCommitBar = new AggregateCommit(barId.RawId, 1, new List<IEvent> { new TestCreateEventBar(barId) }, new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
    };

    Because of = () => migrationOuput = migration.Apply(aggregateCommitBar).ToList();

    It the_migration_output_should_not_be_null = () => migrationOuput.ShouldNotBeNull();
    It the_migration_should_not_change_aggregateCommit_count = () => migrationOuput.Count.ShouldEqual(1);
    It the_migration_output_should_be_same_as_the_input = () => migrationOuput.ShouldContainOnly(aggregateCommitBar);


    static IMigration<AggregateCommit, IEnumerable<AggregateCommit>> migration;
    static AggregateCommit aggregateCommitBar;
    static IList<AggregateCommit> migrationOuput;
}
