using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.Options;
using One.Inception.EventStore.Index;

namespace One.Inception.Hosting;

[Subject("Bootstrap publishing")]
public class When_a_bootstrap_command_cannot_be_published
{
    Establish context = () =>
    {
        log = new ListLogger<EventStoreIndicesStartup>();
        startup = new EventStoreIndicesStartup(
            new TypeContainer<IEventStoreIndex>([typeof(TestIndex)]),
            new HostOptionsMonitor(),
            new TenantsOptionsMonitorMock("pruvit"),
            new RejectingCommandPublisher(),
            log);
    };

    Because of = () => exception = Catch.Exception(() => startup.BootstrapAsync().GetAwaiter().GetResult());

    It should_not_fail_the_bootstrap = () => exception.ShouldBeNull();

    It should_log_which_command_was_not_published = () => log.Errors.ShouldContain(entry => entry.Contains("Failed to publish RegisterIndex") && entry.Contains("pruvit"));

    static EventStoreIndicesStartup startup;
    static ListLogger<EventStoreIndicesStartup> log;
    static Exception exception;

    [DataContract(Name = "8c1d7f0e-3b0a-4e51-9d7e-2f3b5b1f6a10")]
    class TestIndex : IEventStoreIndex
    {
        public Task IndexAsync(InceptionMessage message) => Task.CompletedTask;
    }

    class HostOptionsMonitor : IOptionsMonitor<InceptionHostOptions>
    {
        public InceptionHostOptions CurrentValue { get; } = new InceptionHostOptions();

        public InceptionHostOptions Get(string name) => CurrentValue;

        public IDisposable OnChange(Action<InceptionHostOptions, string> listener) => null;
    }

    class RejectingCommandPublisher : IPublisher<ICommand>
    {
        public Task<bool> PublishAsync(ICommand message, Dictionary<string, string> messageHeaders = null) => Task.FromResult(false);

        public Task<bool> PublishAsync(ICommand message, DateTimeOffset publishAt, Dictionary<string, string> messageHeaders = null) => throw new NotSupportedException();

        public Task<bool> PublishAsync(ICommand message, TimeSpan publishAfter, Dictionary<string, string> messageHeaders = null) => throw new NotSupportedException();

        public Task<bool> PublishAsync(byte[] messageRaw, Type messageType, string tenant, Dictionary<string, string> messageHeaders) => throw new NotSupportedException();
    }
}
