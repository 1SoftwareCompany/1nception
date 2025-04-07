using One.Inception.EventStore;
using System.Threading.Tasks;

namespace One.Inception.Migrations;

public sealed class NoMigrator : IInceptionMigrator
{
    public Task MigrateAsync(AggregateCommit aggregateCommit)
    {
        return Task.CompletedTask;
    }
}
