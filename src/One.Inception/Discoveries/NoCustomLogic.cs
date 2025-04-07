using One.Inception.EventStore;
using One.Inception.Migrations;
using System.Threading.Tasks;

namespace One.Inception.Discoveries;

public class NoCustomLogic : IMigrationCustomLogic
{
    public Task OnAggregateCommitAsync(AggregateCommit migratedAggregateCommit) => Task.CompletedTask;
}
