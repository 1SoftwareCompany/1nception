using System.Threading.Tasks;

namespace One.Inception.EventStore;

public class EmptyAggregateTransformer : IAggregateCommitInterceptor
{
    public Task OnAppendAsync(AggregateCommit origin) => Task.CompletedTask;

    public Task<AggregateCommit> OnAppendingAsync(AggregateCommit origin) => Task.FromResult(origin);
}
