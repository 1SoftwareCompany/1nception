using System;

namespace One.Inception.AtomicAction;

/// <summary>
/// Identifies the aggregate root which is being modified by an atomic action.
/// The aggregate root id is unique only within a bounded context because every bounded context has its own event store.
/// Services which share the same lock storage (ex.: the same redis database) must not block each other.
/// </summary>
public sealed class AtomicActionResource
{
    public AtomicActionResource(string boundedContext, AggregateRootId aggregateRootId)
    {
        if (string.IsNullOrWhiteSpace(boundedContext)) throw new ArgumentException("Bounded context is required.", nameof(boundedContext));
        if (aggregateRootId is null) throw new ArgumentNullException(nameof(aggregateRootId));

        BoundedContext = boundedContext;
        AggregateRootId = aggregateRootId;
        Value = $"{boundedContext}:{aggregateRootId.Value}";
    }

    public string BoundedContext { get; }

    public AggregateRootId AggregateRootId { get; }

    public string Value { get; }

    public override string ToString() => Value;
}
