﻿using System.Collections.Generic;
using System.Threading.Tasks;
using One.Inception.EventStore;

namespace One.Inception.Migrations;

public abstract class MigrationRunnerBase<TDataFormat, TSourceEventStorePlayer, TTargetEventStore>
    where TDataFormat : class
    where TSourceEventStorePlayer : IEventStorePlayer
    where TTargetEventStore : IEventStore
{
    protected readonly IEventStorePlayer source;
    protected readonly IEventStore target;

    public MigrationRunnerBase(TSourceEventStorePlayer source, TTargetEventStore target)
    {
        this.source = source;
        this.target = target;
    }

    /// <summary>
    /// Applies the specified migrations to every <see cref="TDataFormat"/>
    /// </summary>
    /// <param name="migrations"></param>
    public abstract Task RunAsync(IEnumerable<IMigration<TDataFormat>> migrations);
}
