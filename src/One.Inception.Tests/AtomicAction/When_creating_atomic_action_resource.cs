using System;
using Machine.Specifications;
using One.Inception.AtomicAction;
using One.Inception.Tests.TestModel;

namespace One.Inception.Tests.AtomicAction;

[Subject("AtomicActionResource")]
public class When_creating_atomic_action_resource
{
    Establish context = () => id = new TestAggregateId();

    Because of = () => resource = new AtomicActionResource("volumes", id);

    It should_prefix_the_aggregate_root_id_with_the_bounded_context = () => resource.Value.ShouldEqual($"volumes:{id.Value}");

    It should_render_the_value_as_string = () => resource.ToString().ShouldEqual(resource.Value);

    It should_keep_the_aggregate_root_id = () => resource.AggregateRootId.ShouldEqual(id);

    static TestAggregateId id;
    static AtomicActionResource resource;
}

[Subject("AtomicActionResource")]
public class When_creating_atomic_action_resources_for_the_same_aggregate_root_id_in_different_bounded_contexts
{
    Establish context = () => id = new TestAggregateId();

    Because of = () =>
    {
        volumes = new AtomicActionResource("volumes", id);
        relations = new AtomicActionResource("relations", id);
    };

    It should_produce_different_values = () => volumes.Value.ShouldNotEqual(relations.Value);

    static TestAggregateId id;
    static AtomicActionResource volumes;
    static AtomicActionResource relations;
}

[Subject("AtomicActionResource")]
public class When_creating_atomic_action_resource_without_bounded_context
{
    Because of = () => exception = Catch.Exception(() => new AtomicActionResource(" ", new TestAggregateId()));

    It should_fail = () => exception.ShouldBeOfExactType<ArgumentException>();

    static Exception exception;
}

[Subject("AtomicActionResource")]
public class When_creating_atomic_action_resource_without_aggregate_root_id
{
    Because of = () => exception = Catch.Exception(() => new AtomicActionResource("volumes", null));

    It should_fail = () => exception.ShouldBeOfExactType<ArgumentNullException>();

    static Exception exception;
}
