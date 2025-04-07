using System.Runtime.Serialization;
using System.Threading.Tasks;
using One.Inception.EventStore;
using One.Inception.EventStore.Index;

namespace One.Inception.Migrations;

[DataContract(Name = "2f26cd18-0db8-425f-8ada-5e3bf06a57b5")]
public sealed class MigrationHandler : IMigrationHandler,
    IAggregateCommitHandle<AggregateCommit>
{
    private readonly IInceptionMigrator migrator;

    public MigrationHandler(IInceptionMigrator migrator)
    {
        this.migrator = migrator;
    }

    public Task HandleAsync(AggregateCommit aggregateCommit)
    {
        return migrator.MigrateAsync(aggregateCommit);
    }
}
