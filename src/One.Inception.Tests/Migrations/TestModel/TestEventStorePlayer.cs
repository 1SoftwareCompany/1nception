﻿using One.Inception.EventStore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Migration.Middleware.Tests.TestModel;

public class TestEventStorePlayer : IEventStorePlayer
{
    readonly TestEventStore eventStore;
    public TestEventStorePlayer(TestEventStore eventStore)
    {
        if (eventStore is null == true) throw new ArgumentNullException(nameof(eventStore));
        this.eventStore = eventStore;
    }

    public Task EnumerateEventStore(PlayerOperator @operator, PlayerOptions replayOptions, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task EnumerateEventStore(PlayerOperator @operator, PlayerOptions replayOptions)
    {
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<AggregateCommit> LoadAggregateCommitsAsync(int batchSize = 5000)
    {
        // hack
        foreach (var @event in eventStore.Storage)
            yield return @event;
    }


}
