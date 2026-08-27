using System;
using System.Runtime.Serialization;

namespace One.Inception.EventStore.Players;

[DataContract(Name = "64af29f3-228e-49ad-b761-a21188f5d062")]
public sealed class ReplayInternalEventsRequested : ISystemSignal
{
    public ReplayInternalEventsRequested()
    {
        Timestamp = DateTime.Now;
    }

    [DataMember(Order = 0)]
    public string Tenant { get; set; }

    [DataMember(Order = 1)]
    public string SourceEventTypeId { get; set; }

    [DataMember(Order = 2)]
    public string RecipientBoundedContext { get; set; }

    [DataMember(Order = 3)]
    public string RecipientHandlers { get; set; }

    [DataMember(Order = 4)]
    public ReplayEventsOptionNew ReplayOptions { get; set; }

    [DataMember(Order = 5)]
    public DateTimeOffset Timestamp { get; private set; }
}

