using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception;

/// <summary>
/// A publisher with integrated logic to retry on publish failure and log additional data.
/// </summary>
/// <typeparam name="TMessage">The message to be sent.</typeparam>
public abstract class Publisher<TMessage> : PublisherBase<TMessage> where TMessage : IMessage
{
    private RetryPolicy retryPolicy;

    public Publisher(IEnumerable<DelegatingPublishHandler> handlers) : base(handlers)
    {
        retryPolicy = new RetryPolicy(RetryableOperation.RetryPolicyFactory.CreateLinearRetryPolicy(5, TimeSpan.FromMilliseconds(300)));
    }

    public override Task<bool> PublishAsync(TMessage message, Dictionary<string, string> messageHeaders)
    {
        Func<Task<bool>> publishFunc = () => base.PublishAsync(message, messageHeaders);

        return RetryableOperation.TryExecuteAsync(publishFunc, retryPolicy);
    }
}
