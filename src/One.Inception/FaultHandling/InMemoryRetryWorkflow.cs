using System;
using System.Threading.Tasks;
using One.Inception.FaultHandling.Strategies;
using One.Inception.Workflow;
using Microsoft.Extensions.Logging;

namespace One.Inception.FaultHandling;

public class InMemoryRetryWorkflow<TContext> : Workflow<TContext> where TContext : class
{
    private RetryPolicy retryPolicy;

    readonly Workflow<TContext> workflow;

    public InMemoryRetryWorkflow(Workflow<TContext> workflow, ILogger logger)
    {
        this.workflow = workflow;
        ExponentialBackoff retryStrategy = new ExponentialBackoff(5, new TimeSpan(1_280_000), new TimeSpan(100_000_000), new TimeSpan(160_000)); // 128ms, 10000ms, 16ms
        retryPolicy = new RetryPolicy(new TransientErrorCatchAllStrategy(), retryStrategy, logger);
    }

    protected override async Task RunAsync(Execution<TContext> execution)
    {
        if (execution is null) throw new ArgumentNullException(nameof(execution));

        await retryPolicy.ExecuteActionAsync(() => workflow.RunAsync(execution.Context));
    }
}
