using One.Inception.Migration.Middleware.Tests.TestModel.Bar;
using One.Inception.Migration.Middleware.Tests.TestModel.Foo;
using One.Inception.EventStore;
using Machine.Specifications;
using System.Collections.Generic;
using One.Inception.Migrations.TestMigration;
using System;

namespace One.Inception.Migrations;

[Subject("Migration")]
public class When_evalueting_if_migration_should_apply
{
    Establish context = () =>
    {
        migration = new SimpleMigration();
        var fooId = new FooId("1234", "testtenant");
        var barId = new BarId("1234", "testtenant");
        aggregateCommitFoo = new AggregateCommit(fooId.RawId, 1, new List<IEvent>(), new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
        aggregateCommitBar = new AggregateCommit(barId.RawId, 1, new List<IEvent>(), new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
    };

    Because of = () => { };

    It the_evaluation_should_be_true = () => migration.ShouldApply(aggregateCommitFoo).ShouldBeTrue();
    It the_should_apply_should_be_false = () => migration.ShouldApply(aggregateCommitBar).ShouldBeFalse();

    static IMigration<AggregateCommit, IEnumerable<AggregateCommit>> migration;
    static AggregateCommit aggregateCommitFoo;
    static AggregateCommit aggregateCommitBar;
}
