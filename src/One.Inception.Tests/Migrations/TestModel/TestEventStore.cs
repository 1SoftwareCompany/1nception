﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using One.Inception.EventStore;
using One.Inception.EventStore.Index;

namespace One.Inception.Migration.Middleware.Tests.TestModel;

public class TestEventStore : IEventStore
{
    public IList<AggregateCommit> Storage { get; private set; }

    public TestEventStore()
    {
        Storage = new List<AggregateCommit>();
    }

    public Task AppendAsync(AggregateCommit aggregateCommit)
    {
        Storage.Add(aggregateCommit);

        return Task.CompletedTask;
    }


    public Task AppendAsync(AggregateEventRaw aggregateCommitRaw)
    {
        return Task.FromException(new System.NotImplementedException());
    }
    public Task<EventStream> LoadAsync(IBlobId aggregateId)
    {
        var es = new EventStream(Storage.Where(x => x.AggregateRootId.Span.SequenceEqual(aggregateId.RawId.Span)).ToList());
        return Task.FromResult(es);
    }

    public Task<bool> DeleteAsync(AggregateEventRaw eventRaw)
    {
        throw new System.NotImplementedException();
    }
    public Task<LoadAggregateRawEventsWithPagingResult> LoadWithPagingAsync(IBlobId aggregateId, PagingOptions pagingOptions)
    {
        throw new System.NotImplementedException();
    }

    public Task<AggregateEventRaw> LoadAggregateEventRaw(IndexRecord indexRecord)
    {
        throw new System.NotImplementedException();
    }
}
