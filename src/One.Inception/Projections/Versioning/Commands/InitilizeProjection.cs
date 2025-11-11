using System;
using System.Runtime.Serialization;

namespace One.Inception.Projections.Versioning;

[DataContract(Name = "7d8e4a11-c8cd-43de-ba6f-a9a4dd53b636")]
public sealed class InitilizeProjection : ISystemCommand
{
    InitilizeProjection()
    {
        Timestamp = DateTimeOffset.UtcNow;
    }

    public InitilizeProjection(ProjectionVersionManagerId id, string hash) : this()
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrEmpty(hash)) throw new ArgumentNullException(nameof(hash));

        Id = id;
        Hash = hash;
    }

    [DataMember(Order = 1)]
    public ProjectionVersionManagerId Id { get; private set; }

    [DataMember(Order = 2)]
    public string Hash { get; private set; }

    [DataMember(Order = 3)]
    public DateTimeOffset Timestamp { get; private set; }

    public override string ToString()
    {
        return $"Initialize projection with {Hash}. {nameof(ProjectionVersionManagerId)}: {Id}.";
    }
}
