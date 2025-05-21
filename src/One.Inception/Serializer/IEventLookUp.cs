using System;

namespace One.Inception.Serializer;

public interface IEventLookUp
{
    string FindEventId(ReadOnlySpan<byte> data);

    bool HasEventId(ReadOnlySpan<byte> data, ReadOnlySpan<byte> eventId);
}
