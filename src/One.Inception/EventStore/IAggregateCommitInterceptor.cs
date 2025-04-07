using System.Threading.Tasks;

namespace One.Inception.EventStore;

public interface IAggregateCommitInterceptor
{
    Task<AggregateCommit> OnAppendingAsync(AggregateCommit origin);

    Task OnAppendAsync(AggregateCommit origin);
}
