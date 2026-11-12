using System;
using System.Threading.Tasks;
using One.Inception.Workflow;
using One.Inception.MessageProcessing;
using Microsoft.Extensions.Logging;

namespace One.Inception.FaultHandling;

public class InMemoryRetryWorkflow<TContext> : Workflow<TContext> where TContext : class
{
    private RetryPolicy retryPolicy;

    readonly Workflow<TContext> workflow;

    public InMemoryRetryWorkflow(Workflow<TContext> workflow, RetryStrategyFactory fact, ILogger logger)
    {
        this.workflow = workflow;
        RetryStrategy retryStrategy = fact.GetRetryStrategy();
        retryPolicy = new RetryPolicy(new TransientErrorCatchAllStrategy(), retryStrategy, logger);
    }

    protected override async Task RunAsync(Execution<TContext> execution)
    {
        if (execution is null) throw new ArgumentNullException(nameof(execution));

        var cancellationToken = (execution.Context as HandleContext)?.CancellationToken ?? default;
        await retryPolicy.ExecuteActionAsync(() => workflow.RunAsync(execution.Context), cancellationToken);
    }
}
