using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using One.Inception.EventStore;
using One.Inception.Workflow;
using Microsoft.Extensions.Logging;

namespace One.Inception.Migrations;

public class AggregateCommitMigrationWorkflow : MigrationWorkflowBase<AggregateCommit, IEnumerable<AggregateCommit>>
{
    private readonly ILogger<AggregateCommitMigrationWorkflow> logger;

    public AggregateCommitMigrationWorkflow(IMigration<AggregateCommit, IEnumerable<AggregateCommit>> migration, ILogger<AggregateCommitMigrationWorkflow> logger)
        : base(migration)
    {
        this.logger = logger;
    }

    protected override Task<IEnumerable<AggregateCommit>> RunAsync(Execution<AggregateCommit, IEnumerable<AggregateCommit>> context)
    {
        var commit = context.Context;
        IEnumerable<AggregateCommit> newCommits = new List<AggregateCommit> { commit };
        try
        {
            if (migration.ShouldApply(commit))
                newCommits = migration.Apply(commit).ToList();
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Error while applying migration."))) { }

        return Task.FromResult(newCommits);
    }
}
