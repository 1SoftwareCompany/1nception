using System;
using Microsoft.Extensions.Options;
using One.Inception.FaultHandling.Strategies;

namespace One.Inception.FaultHandling;

public class RetryStrategyFactory
{
    private readonly RetryStrategyOptions retryPolicyOptions;

    public RetryStrategyFactory(IOptions<RetryStrategyOptions> provider)
    {
        retryPolicyOptions = provider.Value;
    }

    public static Incremental Default => new Incremental(5, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500));

    public RetryStrategy GetRetryStrategy()
    {
        if (string.IsNullOrEmpty(retryPolicyOptions.Type))
            return Default;

        if (retryPolicyOptions.Type.Equals(ExponentialBackoffRetryOptions.Type) && retryPolicyOptions.ExponentialBackoff is not null)
            return new ExponentialBackoff(retryPolicyOptions.ExponentialBackoff.RetryCount, retryPolicyOptions.ExponentialBackoff.MinBackoff, retryPolicyOptions.ExponentialBackoff.MaxBackoff, retryPolicyOptions.ExponentialBackoff.DeltaBackoff);

        else if (retryPolicyOptions.Type.Equals(IncrementalRetryOptions.Type) && retryPolicyOptions.Incremental is not null)
            return new Incremental(retryPolicyOptions.Incremental.RetryCount, retryPolicyOptions.Incremental.IntialInterval, retryPolicyOptions.Incremental.Increment);

        else if (retryPolicyOptions.Type.Equals(FixedIntervalRetryOptions.Type) && retryPolicyOptions.FixedInterval is not null)
            return new FixedInterval(retryPolicyOptions.FixedInterval.RetryCount, retryPolicyOptions.FixedInterval.RetryInterval);

        return Default;
    }
}
