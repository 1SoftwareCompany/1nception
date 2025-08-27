using Microsoft.Extensions.Configuration;
using One.Inception.FaultHandling.Strategies;
using System;

namespace One.Inception.FaultHandling;

public class RetryStrategyOptionsProvider : InceptionOptionsProviderBase<RetryStrategyOptions>
{
    public RetryStrategyOptionsProvider(IConfiguration configuration) : base(configuration)
    {
    }

    public override void Configure(RetryStrategyOptions options)
    {
        configuration.GetSection("Inception:RetryStrategy").Bind(options);
    }
}

public class RetryStrategyOptions
{
    public string Type { get; set; }

    public FixedIntervalRetryOptions FixedInterval { get; set; }

    public IncrementalRetryOptions Incremental { get; set; }

    public ExponentialBackoffRetryOptions ExponentialBackoff { get; set; }
}

public class FixedIntervalRetryOptions
{
    public static string Type => nameof(FixedInterval);

    public int RetryCount { get; set; }

    public TimeSpan RetryInterval { get; set; }
}

public class IncrementalRetryOptions
{
    public static string Type => nameof(Incremental);

    public int RetryCount { get; set; }

    public TimeSpan Increment { get; set; }

    public TimeSpan IntialInterval { get; set; }
}

public class ExponentialBackoffRetryOptions
{
    public static string Type => nameof(ExponentialBackoff);

    public int RetryCount { get; set; }

    public TimeSpan MinBackoff { get; set; }

    public TimeSpan MaxBackoff { get; set; }

    public TimeSpan DeltaBackoff { get; set; }
}
