using One.Inception.EventStore;
using System.Threading.Tasks;

namespace One.Inception.Migrations;

public interface IMigrationCustomLogic
{
    Task OnAggregateCommitAsync(AggregateCommit migratedAggregateCommit);
}
