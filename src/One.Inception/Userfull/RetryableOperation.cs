using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace One.Inception;

public delegate ShouldRetry RetryPolicy();

public delegate bool ShouldRetry(int retryCount, Exception lastException, out TimeSpan delay);

public static class RetryableOperation
{
    static readonly ILogger logger = InceptionLogger.CreateLogger(typeof(RetryableOperation));

    static RetryPolicy defaultExponentialRetryPolicy = RetryPolicyFactory.CreateExponentialRetryPolicy(5, new TimeSpan(0, 0, 3), new TimeSpan(0, 0, 90), new TimeSpan(0, 0, 6));
    static RetryPolicy defaultLinearRetryPolicy = RetryPolicyFactory.CreateLinearRetryPolicy(5, new TimeSpan(0, 0, 0, 0, 500));

    /// <summary>
    /// Number of retries: 5;
    ///       Min Backoff: 3 seconds;
    ///       Max Backoff: 90 seconds;
    ///              Step: 6 seconds;
    /// </summary>
    public static RetryPolicy DefaultExponentialRetryPolicy { get { return defaultExponentialRetryPolicy; } }

    /// <summary>
    /// Number of retries: 5;
    ///             Delay: 1 second;
    /// </summary>
    public static RetryPolicy DefaultLinearRetryPolicy { get { return defaultLinearRetryPolicy; } }

    public static async Task<T> TryExecuteAsync<T>(Func<Task<T>> operation, RetryPolicy retryPolicy, Func<string> getOperationInfo = null)
    {
        ShouldRetry retry = retryPolicy();
        Exception exception = null;
        T operationResult = default(T);

        TimeSpan delay;
        for (int i = 0; i > -1; i++)
        {
            try
            {
                operationResult = await operation().ConfigureAwait(false);
            }
            catch (Exception ex) { exception = ex; }

            if (operationResult is not null || operationResult.Equals(default(T)) == false)
                break;

            if (retry(i, exception, out delay))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Retry {retryCount} after {delay}ms. Operation Info: {operationInfo}", i, delay.TotalMilliseconds, getOperationInfo());

                await Task.Delay(delay).ConfigureAwait(false);
            }
            else
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Maximum number of retries has been reached.");

                if (exception is null)
                    exception = new Exception($"Maximum number of retries has been reached.{Environment.NewLine}{getOperationInfo()}");

                throw exception;
            }
        }

        return operationResult;
    }

    public class RetryPolicyFactory
    {
        public static RetryPolicy CreateExponentialRetryPolicy(int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
        {
            return () =>
            {
                return (int currentRetryCount, Exception lastException, out TimeSpan retryInterval) =>
                {
                    if (currentRetryCount < retryCount)
                    {
                        Random rand = new Random();
                        int increment = (int)((Math.Pow(2, currentRetryCount) - 1) * rand.Next((int)(deltaBackoff.TotalMilliseconds * 0.8), (int)(deltaBackoff.TotalMilliseconds * 1.2)));
                        int timeToSleepMsec = (int)Math.Min(minBackoff.TotalMilliseconds + increment, maxBackoff.TotalMilliseconds);

                        retryInterval = TimeSpan.FromMilliseconds(timeToSleepMsec);

                        return true;
                    }

                    retryInterval = TimeSpan.Zero;
                    return false;
                };
            };
        }

        public static RetryPolicy CreateLinearRetryPolicy(int retryCount, TimeSpan intervalBetweenRetries)
        {
            return () =>
            {
                return (int currentRetryCount, Exception lastException, out TimeSpan delay) =>
                {
                    delay = intervalBetweenRetries;
                    return currentRetryCount < retryCount;
                };
            };
        }

        public static RetryPolicy CreateInfiniteLinearRetryPolicy(TimeSpan intervalBetweenRetries)
        {
            return () =>
            {
                return (int currentRetryCount, Exception lastException, out TimeSpan delay) =>
                {
                    delay = intervalBetweenRetries;
                    return true;

                };
            };
        }
    }
}
