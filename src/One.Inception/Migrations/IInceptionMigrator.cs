using System.Threading.Tasks;
using One.Inception.EventStore;

namespace One.Inception.Migrations;

public interface IInceptionMigrator
{
    Task MigrateAsync(AggregateCommit aggregateCommit);
}

public interface IInceptionMigratorManual : IInceptionMigrator { }
