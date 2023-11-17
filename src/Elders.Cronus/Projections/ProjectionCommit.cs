﻿using System;
using System.Runtime.Serialization;

namespace Elders.Cronus.Projections
{
    [DataContract(Name = "db13e442-a6d2-4247-9e5f-86931907f00b")]
    public class ProjectionCommitPreview
    {
        ProjectionCommitPreview() { }

        public ProjectionCommitPreview(IBlobId projectionId, ProjectionVersion version, IEvent @event)
        {
            ProjectionId = projectionId;
            Event = @event;
            Version = version;
        }

        [DataMember(Order = 1)]
        public IBlobId ProjectionId { get; private set; }

        [DataMember(Order = 2)]
        public ProjectionVersion Version { get; private set; }

        [DataMember(Order = 3)]
        public IEvent Event { get; private set; }
    }

    [DataContract(Name = "ed0d9b4e-3ac5-4cd4-9598-7bf5687b037a")]
    public class ProjectionCommit
    {
        ProjectionCommit() { }

        public ProjectionCommit(IBlobId projectionId, ProjectionVersion version, IEvent @event, EventOrigin eventOrigin, DateTime timeStamp)
        {
            ProjectionId = projectionId;
            ProjectionName = version.ProjectionName;
            Event = @event;
            EventOrigin = eventOrigin;
            TimeStamp = timeStamp;
            Version = version;
        }

        [DataMember(Order = 1)]
        public IBlobId ProjectionId { get; private set; }

        [DataMember(Order = 3)]
        public IEvent Event { get; private set; }

        [DataMember(Order = 4)]
        public int SnapshotMarker { get; set; }

        [DataMember(Order = 5)]
        public EventOrigin EventOrigin { get; private set; }

        [DataMember(Order = 6)]
        public DateTime TimeStamp { get; private set; }

        [DataMember(Order = 7)]
        public string ProjectionName { get; private set; }

        [DataMember(Order = 8)]
        public ProjectionVersion Version { get; private set; }
    }
}
