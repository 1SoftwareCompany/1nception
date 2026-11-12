using System;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using One.Inception.Projections;

namespace One.Inception.Tests.MessageProcessing;

[Subject("Projection cancellation")]
public class When_projection_restoration_is_cancelled
{
    Because of = () =>
    {
        using var source = new CancellationTokenSource();
        var projection = new CancellationProjection(source);
        var stream = new ProjectionStream(
            new ProjectionVersion("test", ProjectionStatus.Live, 1, "test"),
            new Urn("urn:test:projection:1"),
            new[] { new CancellationEvent(), new CancellationEvent() });
        try { stream.RestoreFromHistoryAsync(projection, source.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { cancelled = true; }
        calls = projection.Calls;
    };

    It should_stop_before_applying_the_next_event = () => calls.ShouldEqual(1);
    It should_propagate_cancellation = () => cancelled.ShouldBeTrue();

    static bool cancelled;
    static int calls;
}

public class CancellationProjection : ProjectionDefinition<object, Urn>, IEventHandle<CancellationEvent>
{
    private readonly CancellationTokenSource source;
    public int Calls;

    public CancellationProjection(CancellationTokenSource source) { this.source = source; }

    public Task HandleAsync(CancellationEvent message, CancellationToken cancellationToken)
    {
        Calls++;
        source.Cancel();
        return Task.CompletedTask;
    }
}

public class CancellationEvent : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}
