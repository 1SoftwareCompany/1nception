﻿using System;
using System.Threading.Tasks;
using One.Inception.FaultHandling.Strategies;
using One.Inception.MessageProcessing;
using Microsoft.Extensions.Logging;

namespace One.Inception.FaultHandling;

/// <summary>
/// Provides the base implementation of the retry mechanism for unreliable actions and transient conditions.
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Returns a default policy that does no retries, it just invokes action exactly once.
    /// </summary>
    public static readonly RetryPolicy NoRetry = new RetryPolicy(new TransientErrorIgnoreStrategy(), 0);

    /// <summary>
    /// Returns a default policy that implements a fixed retry interval configured with the default <see cref="FixedInterval"/> retry strategy.
    /// The default retry policy treats all caught exceptions as transient errors.
    /// </summary>
    public static readonly RetryPolicy DefaultFixed = new RetryPolicy(new TransientErrorCatchAllStrategy(), new FixedInterval());

    /// <summary>
    /// Returns a default policy that implements a progressive retry interval configured with the default <see cref="Incremental"/> retry strategy.
    /// The default retry policy treats all caught exceptions as transient errors.
    /// </summary>
    public static readonly RetryPolicy DefaultProgressive = new RetryPolicy(new TransientErrorCatchAllStrategy(), new Incremental());

    /// <summary>
    /// Returns a default policy that implements a random exponential retry interval configured with the default <see cref="FixedInterval"/> retry strategy.
    /// The default retry policy treats all caught exceptions as transient errors.
    /// </summary>
    public static readonly RetryPolicy DefaultExponential = new RetryPolicy(new TransientErrorCatchAllStrategy(), new ExponentialBackoff());
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the RetryPolicy class with the specified number of retry attempts and parameters defining the progressive delay between retries.
    /// </summary>
    /// <param name="errorDetectionStrategy">The <see cref="ITransientErrorDetectionStrategy"/> that is responsible for detecting transient conditions.</param>
    /// <param name="retryStrategy">The retry strategy to use for this retry policy.</param>
    public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, RetryStrategy retryStrategy)
    {
        //Guard.ArgumentNotNull(errorDetectionStrategy, "errorDetectionStrategy");
        //Guard.ArgumentNotNull(retryStrategy, "retryPolicy");

        this.ErrorDetectionStrategy = errorDetectionStrategy;

        if (errorDetectionStrategy == null)
        {
            throw new InvalidOperationException("The error detection strategy type must implement the ITransientErrorDetectionStrategy interface.");
        }

        this.RetryStrategy = retryStrategy;
    }

    /// <summary>
    /// Initializes a new instance of the RetryPolicy class with the specified number of retry attempts and parameters defining the progressive delay between retries.
    /// </summary>
    /// <param name="errorDetectionStrategy">The <see cref="ITransientErrorDetectionStrategy"/> that is responsible for detecting transient conditions.</param>
    /// <param name="retryStrategy">The retry strategy to use for this retry policy.</param>
    public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, RetryStrategy retryStrategy, ILogger logger)
    {
        //Guard.ArgumentNotNull(errorDetectionStrategy, "errorDetectionStrategy");
        //Guard.ArgumentNotNull(retryStrategy, "retryPolicy");

        this.ErrorDetectionStrategy = errorDetectionStrategy;

        if (errorDetectionStrategy == null)
        {
            throw new InvalidOperationException("The error detection strategy type must implement the ITransientErrorDetectionStrategy interface.");
        }

        this.RetryStrategy = retryStrategy;
        this.logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the RetryPolicy class with the specified number of retry attempts and default fixed time interval between retries.
    /// </summary>
    /// <param name="errorDetectionStrategy">The <see cref="ITransientErrorDetectionStrategy"/> that is responsible for detecting transient conditions.</param>
    /// <param name="retryCount">The number of retry attempts.</param>
    public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount)
        : this(errorDetectionStrategy, new FixedInterval(retryCount))
    {
    }

    /// <summary>
    /// Initializes a new instance of the RetryPolicy class with the specified number of retry attempts and fixed time interval between retries.
    /// </summary>
    /// <param name="errorDetectionStrategy">The <see cref="ITransientErrorDetectionStrategy"/> that is responsible for detecting transient conditions.</param>
    /// <param name="retryCount">The number of retry attempts.</param>
    /// <param name="retryInterval">The interval between retries.</param>
    public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount, TimeSpan retryInterval)
        : this(errorDetectionStrategy, new FixedInterval(retryCount, retryInterval))
    {
    }

    /// <summary>
    /// Initializes a new instance of the RetryPolicy class with the specified number of retry attempts and back-off parameters for calculating the exponential delay between retries.
    /// </summary>
    /// <param name="errorDetectionStrategy">The <see cref="ITransientErrorDetectionStrategy"/> that is responsible for detecting transient conditions.</param>
    /// <param name="retryCount">The number of retry attempts.</param>
    /// <param name="minBackoff">The minimum back-off time.</param>
    /// <param name="maxBackoff">The maximum back-off time.</param>
    /// <param name="deltaBackoff">The time value that will be used for calculating a random delta in the exponential delay between retries.</param>
    public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
        : this(errorDetectionStrategy, new ExponentialBackoff(retryCount, minBackoff, maxBackoff, deltaBackoff))
    {
    }

    /// <summary>
    /// Initializes a new instance of the RetryPolicy class with the specified number of retry attempts and parameters defining the progressive delay between retries.
    /// </summary>
    /// <param name="errorDetectionStrategy">The <see cref="ITransientErrorDetectionStrategy"/> that is responsible for detecting transient conditions.</param>
    /// <param name="retryCount">The number of retry attempts.</param>
    /// <param name="initialInterval">The initial interval that will apply for the first retry.</param>
    /// <param name="increment">The incremental time value that will be used for calculating the progressive delay between retries.</param>
    public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount, TimeSpan initialInterval, TimeSpan increment)
        : this(errorDetectionStrategy, new Incremental(retryCount, initialInterval, increment))
    {
    }

    /// <summary>
    /// An instance of a callback delegate that will be invoked whenever a retry condition is encountered.
    /// </summary>
    public event EventHandler<RetryingEventArgs> Retrying;

    /// <summary>
    /// Gets the retry strategy.
    /// </summary>
    public RetryStrategy RetryStrategy { get; private set; }

    /// <summary>
    /// Gets the instance of the error detection strategy.
    /// </summary>
    public ITransientErrorDetectionStrategy ErrorDetectionStrategy { get; private set; }

    /// <summary>
    /// Repetitively executes the specified action while it satisfies the current retry policy.
    /// </summary>
    /// <typeparam name="TResult">The type of result expected from the executable action.</typeparam>
    /// <param name="func">A delegate representing the executable action which returns the result of type R.</param>
    /// <returns>The result from the action.</returns>
    public virtual async Task<TResult> ExecuteActionAsync<TResult>(Func<Task<TResult>> func)
    {
        //Guard.ArgumentNotNull(func, "func");

        int retryCount = 0;
        TimeSpan delay = TimeSpan.Zero;
        Exception lastError;

        var shouldRetry = this.RetryStrategy.GetShouldRetry();

        while (true)
        {
            lastError = null;

            try
            {
                var result = await func();
                return result;
            }
            catch (RetryLimitExceededException limitExceededEx)
            {
                // The user code can throw a RetryLimitExceededException to force the exit from the retry loop.
                // The RetryLimitExceeded exception can have an inner exception attached to it. This is the exception
                // which we will have to throw up the stack so that callers can handle it.
                if (limitExceededEx.InnerException != null)
                {
                    throw limitExceededEx.InnerException;
                }
                else
                {
                    return default;
                }
            }
            catch (WorkflowExecutionException ex) when ((ErrorDetectionStrategy.IsTransient(ex) && shouldRetry(retryCount++, ex, out delay)) && True(() => logger.LogWarning(ex, "Operation has failed. Retrying - Count: {retryCount}, Delay: {delay}ms.", retryCount, delay.TotalMilliseconds)))
            {
                // Retry
                lastError = ex;
            }
            catch (WorkflowExecutionException ex) when (((ErrorDetectionStrategy.IsTransient(ex) && shouldRetry(retryCount++, ex, out _)) == false) && True(() => logger.LogError(ex, "Operation has failed.")))
            {
                // Error
                lastError = ex;
                throw;
            }
            // If there is another exception we will let it pop because most probably this is non-user exception and there is no need for a retry. (There is a inception bug. Figure out how and fix it.): PAFA

            // We are here because we should retry. 
            // However, we will perform an extra check in the delay interval. Should prevent from accidentally ending up with the value of -1 that will block a thread indefinitely.
            // In addition, any other negative numbers will cause an ArgumentOutOfRangeException fault that will be thrown by Thread.Sleep.
            if (delay.TotalMilliseconds < 0)
            {
                delay = TimeSpan.Zero;
            }

            OnRetrying(retryCount, lastError, delay);

            if (retryCount > 1 || RetryStrategy.FastFirstRetry == false)
            {
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Notifies the subscribers whenever a retry condition is encountered.
    /// </summary>
    /// <param name="retryCount">The current retry attempt count.</param>
    /// <param name="lastError">The exception which caused the retry conditions to occur.</param>
    /// <param name="delay">The delay indicating how long the current thread will be suspended for before the next iteration will be invoked.</param>
    protected virtual void OnRetrying(int retryCount, Exception lastError, TimeSpan delay)
    {
        if (Retrying is not null)
        {
            Retrying(this, new RetryingEventArgs(retryCount, delay, lastError));
        }
    }
}
