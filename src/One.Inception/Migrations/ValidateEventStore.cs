﻿using System.Collections.Generic;
using System.Threading.Tasks;
using One.Inception.EventStore;
using Microsoft.Extensions.Logging;

namespace One.Inception.Migrations;

public sealed class ValidateEventStore<TSourceEventStorePlayer, TTargetEventStore> : MigrationRunnerBase<AggregateEventRaw, TSourceEventStorePlayer, TTargetEventStore>
    where TSourceEventStorePlayer : IEventStorePlayer
    where TTargetEventStore : IEventStore
{
    private readonly ILogger logger;

    public ValidateEventStore(TSourceEventStorePlayer source, TTargetEventStore target, ILogger logger) : base(source, target)
    {
        this.logger = logger;
    }

    public override async Task RunAsync(IEnumerable<IMigration<AggregateEventRaw>> migrations)
    {
        PlayerOperator @operator = new PlayerOperator()
        {
            OnLoadAsync = raw => target.AppendAsync(raw)
        };

        PlayerOptions playerOptions = new PlayerOptions();
        await source.EnumerateEventStore(@operator, playerOptions);
    }
}
