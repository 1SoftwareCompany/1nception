using System;
using System.Collections.Generic;
using System.Text;
using One.Inception.EventStore;

namespace One.Inception.Migrations.TestMigration;

public class SimpleMigration : IMigration<AggregateCommit, IEnumerable<AggregateCommit>>
{
    readonly string targetAggregateName = "Foo".ToLowerInvariant();

    public IEnumerable<AggregateCommit> Apply(AggregateCommit current)
    {
        if (ShouldApply(current))
            throw new NotImplementedException();
        else
            yield return current;
    }

    public bool ShouldApply(AggregateCommit current)
    {
        string currentAggregateName = Encoding.UTF8.GetString(current.AggregateRootId.Span).Split(':')[2].ToLowerInvariant();

        if (currentAggregateName == targetAggregateName)
            return true;

        return false;
    }
}
