using System;

namespace One.Inception.EventStore;

public sealed class MissingSerializer : ISerializer
{
    public T DeserializeFromBytes<T>(byte[] bytes) => throw new NotImplementedException();

    public byte[] SerializeToBytes<T>(T message) => throw new NotImplementedException();

    public string SerializeToString<T>(T message) => throw new NotImplementedException();
}
