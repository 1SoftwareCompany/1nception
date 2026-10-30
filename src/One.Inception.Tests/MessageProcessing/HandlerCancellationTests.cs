using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.Logging.Abstractions;
using One.Inception.FaultHandling;
using One.Inception.FaultHandling.Strategies;
using One.Inception.MessageProcessing;
using One.Inception.Workflow;

namespace One.Inception.Tests.MessageProcessing;

[Subject("Handler cancellation")]
public class When_a_subscriber_is_cancelled
{
    Establish context = () =>
    {
        source = new CancellationTokenSource();
        handler = new CancellationHandler();
        workflow = new MessageHandleWorkflow(
            WorkflowExtensions.Lambda<HandleContext, IHandlerInstance>(
                _ => Task.FromResult<IHandlerInstance>(new DefaultHandlerInstance(handler))));
        workflow.Finalize.Use(_ => { finalized = true; return Task.CompletedTask; });
        workflow.Error.Use(_ => { errorCalled = true; return Task.CompletedTask; });
        subscriber = new HandlerSubscriber(typeof(CancellationHandler),
            new ExceptionEaterWorkflow<HandleContext>(workflow));
    };

    Because of = () => RunAsync().GetAwaiter().GetResult();

    static async Task RunAsync()
    {
        Task processing = subscriber.ProcessAsync(
            new InceptionMessage(new CancellationCommand(), new Dictionary<string, string>()), source.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Cancel();
        try { await processing.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException ex) { cancellation = ex; }
    }

    It should_pass_the_source_token = () => handler.Token.ShouldEqual(source.Token);
    It should_not_wrap_or_swallow_cancellation = () => cancellation.ShouldNotBeNull();
    It should_finalize = () => finalized.ShouldBeTrue();
    It should_not_run_the_error_workflow = () => errorCalled.ShouldBeFalse();
    It should_dispose_the_handler = () => handler.Disposed.ShouldBeTrue();

    Cleanup after = () => source.Dispose();

    static CancellationTokenSource source;
    static CancellationHandler handler;
    static MessageHandleWorkflow workflow;
    static HandlerSubscriber subscriber;
    static OperationCanceledException cancellation;
    static bool finalized;
    static bool errorCalled;
}

public class CancellationCommand : ICommand
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

public class CancellationHandler : ICommandHandle<CancellationCommand>, IDisposable
{
    public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken Token;
    public bool Disposed;

    public async Task HandleAsync(CancellationCommand command, CancellationToken cancellationToken)
    {
        Token = cancellationToken;
        Started.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    public void Dispose() => Disposed = true;
}

[Subject("Handler cancellation")]
public class When_shutdown_interrupts_a_retry_delay
{
    Because of = () => RunAsync().GetAwaiter().GetResult();

    static async Task RunAsync()
    {
        using var source = new CancellationTokenSource();
        var retryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retry = new One.Inception.FaultHandling.RetryPolicy(new TransientErrorCatchAllStrategy(),
            new FixedInterval("test", 5, TimeSpan.FromMinutes(1), false), NullLogger.Instance);
        retry.Retrying += (_, _) => retryStarted.TrySetResult();

        Task processing = retry.ExecuteActionAsync<object>(() =>
        {
            attempts++;
            throw new WorkflowExecutionException();
        }, source.Token);

        await retryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Cancel();
        try { await processing.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { cancelled = true; }
    }

    It should_cancel_without_waiting_for_the_retry_interval = () => cancelled.ShouldBeTrue();
    It should_not_retry_after_shutdown = () => attempts.ShouldEqual(1);

    static int attempts;
    static bool cancelled;
}
