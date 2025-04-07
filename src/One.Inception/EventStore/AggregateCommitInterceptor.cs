using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception.EventStore;

public sealed class AggregateCommitInterceptor : IAggregateCommitInterceptor
{
    private readonly IEnumerable<IAggregateCommitInterceptor> interceptors;

    public AggregateCommitInterceptor(IEnumerable<IAggregateCommitInterceptor> interceptor)
    {
        this.interceptors = interceptor;
    }

    public async Task OnAppendAsync(AggregateCommit origin)
    {
        foreach (var interceptor in interceptors)
        {
            await interceptor.OnAppendAsync(origin).ConfigureAwait(false);
        }
    }

    public async Task<AggregateCommit> OnAppendingAsync(AggregateCommit origin)
    {
        AggregateCommit transformedCommit = new AggregateCommit(origin);

        foreach (var interceptor in interceptors)
        {
            transformedCommit = await interceptor.OnAppendingAsync(transformedCommit).ConfigureAwait(false);
        }

        return transformedCommit;
    }
}
