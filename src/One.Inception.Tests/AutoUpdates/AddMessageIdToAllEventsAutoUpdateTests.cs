using System;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.Logging.Abstractions;
using One.Inception.AutoUpdates;
using One.Inception.EventStore;

namespace One.Inception.Tests.AutoUpdates;

[Subject("AutoUpdate")]
public class When_add_message_id_auto_update_is_canceled_during_the_enumeration
{
    Establish context = () =>
    {
        cancellation = new CancellationTokenSource();
        autoUpdate = new AddMessageIdToAllEventsAutoUpdate(new EnumerationStub(cancellation), null, null, null, NullLogger<AddMessageIdToAllEventsAutoUpdate>.Instance);
    };

    Because of = () => expectedException = Catch.Exception(() => autoUpdate.ApplyAsync(cancellation.Token).GetAwaiter().GetResult());

    It should_not_report_the_update_as_finished = () => expectedException.ShouldBeAssignableTo<OperationCanceledException>();

    static CancellationTokenSource cancellation;
    static AddMessageIdToAllEventsAutoUpdate autoUpdate;
    static Exception expectedException;
}

[Subject("AutoUpdate")]
public class When_add_message_id_auto_update_enumerates_everything
{
    Establish context = () => autoUpdate = new AddMessageIdToAllEventsAutoUpdate(new EnumerationStub(null), null, null, null, NullLogger<AddMessageIdToAllEventsAutoUpdate>.Instance);

    Because of = () => finished = autoUpdate.ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();

    It should_report_the_update_as_finished = () => finished.ShouldBeTrue();

    static AddMessageIdToAllEventsAutoUpdate autoUpdate;
    static bool finished;
}

class EnumerationStub : IEventStorePlayer
{
    private readonly CancellationTokenSource cancellation;

    public EnumerationStub(CancellationTokenSource cancellation)
    {
        this.cancellation = cancellation;
    }

    // Like the real store, it stops silently when canceled.
    public Task EnumerateEventStore(PlayerOperator @operator, PlayerOptions replayOptions, CancellationToken cancellationToken = default)
    {
        cancellation?.Cancel();
        return Task.CompletedTask;
    }
}
