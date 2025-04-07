﻿using One.Inception.Projections;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace One.Inception.Tests.Projections;

[DataContract(Name = "05a82e14-3bcd-4e0e-a725-65f3d3a0ee0e")]
public class TestProjection : ProjectionDefinition<TestProjectionState, TestProjectionId>,
    IEventHandler<TestEvent1>,
    IEventHandler<TestEvent2>,
    IEventHandler<TestEvent3>
{
    public Task HandleAsync(TestEvent1 @event) { return Task.CompletedTask; }

    public Task HandleAsync(TestEvent2 @event) { return Task.CompletedTask; }

    public Task HandleAsync(TestEvent3 @event) { return Task.CompletedTask; }

    [DataMember(Order = 1)]
    public string Text { get; set; }
}

[DataContract(Name = "05a82e14-3bcd-4e0e-a725-65f3d3a0ee0e")]
public class TestProjectionShuffled : ProjectionDefinition<TestProjectionState, TestProjectionId>,
    IEventHandler<TestEvent2>,
    IEventHandler<TestEvent3>,
    IEventHandler<TestEvent1>
{
    public Task HandleAsync(TestEvent1 @event) { return Task.CompletedTask; }

    public Task HandleAsync(TestEvent2 @event) { return Task.CompletedTask; }

    public Task HandleAsync(TestEvent3 @event) { return Task.CompletedTask; }

    [DataMember(Order = 1)]
    public string Text { get; set; }
}

[DataContract(Name = "05a82e14-3bcd-4e0e-a725-65f3d3a0ee0e")]
public class TestProjectionModified : ProjectionDefinition<TestProjectionState, TestProjectionId>,
    IEventHandler<TestEvent2>,
    IEventHandler<TestEvent1>
{
    public Task HandleAsync(TestEvent1 @event) { return Task.CompletedTask; }

    public Task HandleAsync(TestEvent2 @event) { return Task.CompletedTask; }

    [DataMember(Order = 1)]
    public string Text { get; set; }
}

[DataContract(Name = "asc")]
public class TestProjectionHandlersAsc : ProjectionDefinition<TestProjectionState, TestProjectionId>,
    IEventHandler<A>,
    IEventHandler<B>
{
    public Task HandleAsync(A @event) => Task.CompletedTask;
    public Task HandleAsync(B @event) => Task.CompletedTask;
}

[DataContract(Name = "desc")]
public class TestProjectionHandlersDesc : ProjectionDefinition<TestProjectionState, TestProjectionId>,
    IEventHandler<B>,
    IEventHandler<A>
{
    public Task HandleAsync(B @event) => Task.CompletedTask;
    public Task HandleAsync(A @event) => Task.CompletedTask;
}

public class TestProjectionState
{

}

public class TestProjectionId : AggregateRootId
{

}

[DataContract(Name = "25061980-5057-475f-b734-2c4a6b52286f")]
public class TestEvent1 : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

[DataContract(Name = "833bedee-0109-402b-81de-29986bd46221")]
public class TestEvent2 : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

[DataContract(Name = "7898a318-c8e5-4be5-b1e3-13c4f5da28d5")]
public class TestEvent3 : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

[DataContract(Name = "NonVersionableProjection")]
public class NonVersionableProjection : IProjection, INonVersionableProjection { }

[DataContract(Name = "INonReplayableProjection")]
public class NonReplayableProjection : IProjection, INonVersionableProjection { }

[DataContract(Name = "INonRebuildableProjection")]
public class NonRebuildableProjection : IProjection, INonRebuildableProjection { }


[DataContract(Name = "a95d15cc-d29d-4a40-8df4-a6ebf837fb98")]
public class A : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

[DataContract(Name = "bf2aed4c-d192-445b-9bc0-b3bebbef1c83")]
public class B : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}
