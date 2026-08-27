using System;
using System.Runtime.Serialization;

namespace One.Inception.EventStore.Players;

[DataContract(Name = "a4a29dd3-4dfd-4b1c-941f-02760ac23576")]
public sealed class ReplayEventsOptions
{
    [DataMember(Order = 1)]
    public DateTimeOffset? After { get; set; }

    [DataMember(Order = 2)]
    public DateTimeOffset? Before { get; set; }

    [DataMember(Order = 3)]
    public int MaxDegreeOfParallelism { get; set; } = 2;
}

[DataContract(Name = "d4458d06-a0b2-4e8a-8fa8-0ba1f07235e5")]
public sealed class ReplayEventsOptionNew
{
    [DataMember(Order = 1)]
    public DateTimeOffset? After { get; set; }

    [DataMember(Order = 2)]
    public DateTimeOffset? Before { get; set; }

    [DataMember(Order = 3)]
    public Urn AggregateRootId { get; set; }

    [DataMember(Order = 4)]
    public bool ShouldReplayLastEventOnly { get; set; }

    [DataMember(Order = 5)]
    public int MaxDegreeOfParallelism { get; set; } = 2;
}
