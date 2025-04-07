using System.Collections.Generic;
using System.Threading.Tasks;
using One.Inception.EventStore;
using Microsoft.Extensions.Logging;

namespace One.Inception.Migrations;

public class CopyEventStore<TSourceEventStorePlayer, TTargetEventStore> : MigrationRunnerBase<AggregateEventRaw, TSourceEventStorePlayer, TTargetEventStore>
    where TSourceEventStorePlayer : IEventStorePlayer
    where TTargetEventStore : IEventStore
{
    public CopyEventStore(TSourceEventStorePlayer source, TTargetEventStore target, ILogger logger) : base(source, target)
    {
    }

    public override async Task RunAsync(IEnumerable<IMigration<AggregateEventRaw>> migrations)
    {
        PlayerOperator @operator = new PlayerOperator()
        {
            OnLoadAsync = target.AppendAsync
        };

        PlayerOptions playerOptions = new PlayerOptions();
        await source.EnumerateEventStore(@operator, playerOptions);
    }
}
