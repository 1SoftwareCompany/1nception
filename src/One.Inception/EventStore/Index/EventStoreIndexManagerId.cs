﻿using System.Runtime.Serialization;

namespace One.Inception.EventStore.Index;

[DataContract(Name = "b11705a2-6744-4ca6-8480-e887c3fc09f2")]
public class EventStoreIndexManagerId : AggregateRootId
{
    EventStoreIndexManagerId() : base() { }

    public EventStoreIndexManagerId(string indexName, string tenant) : base(tenant, "eventstoreindexmanager", indexName) { }
}
