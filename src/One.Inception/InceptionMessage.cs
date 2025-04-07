using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace One.Inception;

[DataContract(Name = "71a0dc2e-1d59-4818-af05-222b334fffbe")]
public sealed class InceptionMessage : IEquatable<InceptionMessage>
{
    InceptionMessage()
    {
        Headers = new Dictionary<string, string>();
    }

    public InceptionMessage(byte[] message, Type messageType, IDictionary<string, string> headers) : this()
    {
        if (messageType is null) throw new ArgumentNullException(nameof(messageType));

        Id = Guid.NewGuid();
        PayloadRaw = message ?? throw new ArgumentNullException(nameof(message));
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        Headers.TryAdd(MessageHeader.MessageType, messageType.GetContractId());
    }

    public InceptionMessage(IMessage message, IDictionary<string, string> headers) : this()
    {
        Id = Guid.NewGuid();
        Payload = message ?? throw new ArgumentNullException(nameof(message));
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
    }

    [DataMember(Order = 1)]
    public Guid Id { get; private set; }

    [DataMember(Order = 2)]
    public IMessage Payload { get; private set; }

    [DataMember(Order = 3)]
    public IDictionary<string, string> Headers { get; private set; }

    [DataMember(Order = 4)]
    public byte[] PayloadRaw { get; private set; }

    public Type GetMessageType()
    {
        Type messageType = Payload is not null
            ? Payload.GetType()
            : Headers[MessageHeader.MessageType].GetTypeByContract();

        return messageType;
    }

    public string[] RecipientHandlers
    {
        get
        {
            if (HasHeader(MessageHeader.RecipientHandlers))
            {
                return GetHeader(MessageHeader.RecipientHandlers).Split(',');
            }
            return Array.Empty<string>();
        }
    }

    public bool IsRepublished => RecipientHandlers.Any();

    public string BoundedContext => GetHeader(MessageHeader.BoundedContext);

    public string Tenant => GetHeader(MessageHeader.Tenant);

    public string RecipientBoundedContext
    {
        get
        {
            if (HasHeader(MessageHeader.RecipientBoundedContext))
            {
                return GetHeader(MessageHeader.RecipientBoundedContext);
            }
            return BoundedContext;
        }
    }

    string GetHeader(string key)
    {
        string value;
        if (Headers.TryGetValue(key, out value) == false)
            value = string.Empty;

        return value;
    }

    bool HasHeader(string key)
    {
        return Headers.ContainsKey(key);
    }

    public override bool Equals(System.Object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (!typeof(InceptionMessage).IsAssignableFrom(obj.GetType())) return false;
        return Equals((InceptionMessage)obj);
    }

    public bool Equals(InceptionMessage other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return this.Id == other.Id;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return 103 ^ this.Id.GetHashCode();
        }
    }

    public static bool operator ==(InceptionMessage left, InceptionMessage right)
    {
        if (left is null && right is null) return true;
        if (left is null)
            return false;
        else
            return left.Equals(right);
    }

    public static bool operator !=(InceptionMessage a, InceptionMessage b)
    {
        return !(a == b);
    }
}
