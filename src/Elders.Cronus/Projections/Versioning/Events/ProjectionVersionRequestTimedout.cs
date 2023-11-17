﻿using System;
using System.Runtime.Serialization;

namespace Elders.Cronus.Projections.Versioning
{
    [DataContract(Namespace = "cronus", Name = "1d249c16-555c-4463-92dc-54218d9a3245")]
    public sealed class ProjectionVersionRequestTimedout : ISystemEvent
    {
        ProjectionVersionRequestTimedout() { }

        public ProjectionVersionRequestTimedout(ProjectionVersionManagerId id, ProjectionVersion version, VersionRequestTimebox timebox)
        {
            Id = id;
            Version = version;
            RequestTimestamp = DateTimeOffset.UtcNow.ToFileTime();
            Timebox = timebox;
        }

        [DataMember(Order = 1)]
        public ProjectionVersionManagerId Id { get; private set; }

        [DataMember(Order = 2)]
        public ProjectionVersion Version { get; private set; }

        [DataMember(Order = 3)]
        public long RequestTimestamp { get; private set; }

        [DataMember(Order = 4)]
        public VersionRequestTimebox Timebox { get; private set; }

        public DateTimeOffset Timestamp => DateTimeOffset.FromFileTime(RequestTimestamp);

        public override string ToString()
        {
            return $"Timeout projection version requested for `{Version}` with timebox `{Timebox}`";
        }
    }
}
