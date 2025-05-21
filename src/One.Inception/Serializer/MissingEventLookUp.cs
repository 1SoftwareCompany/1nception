using System;
using One.Inception.Serializer;

namespace One.Inception.EventStore;

public sealed class MissingEventLookUp : IEventLookUp
{
    public string FindEventId(ReadOnlySpan<byte> data) => throw new NotImplementedException();

    public bool HasEventId(ReadOnlySpan<byte> data, ReadOnlySpan<byte> eventId) => throw new NotImplementedException();
}
