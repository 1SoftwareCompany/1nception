using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Machine.Specifications;
using One.Inception.Tests.TestModel;

namespace One.Inception.Hosting;

[Subject("Bootstrap publishing")]
public class When_a_bootstrap_command_fails_to_publish_a_few_times
{
    Establish context = () =>
    {
        BootstrapPublishing.RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero];
        publisher = new FlakyCommandPublisher(failures: 2);
    };

    Because of = () => exception = Catch.Exception(() => publisher.PublishOrThrowAsync(new TestCreateCommand(new TestAggregateId())).GetAwaiter().GetResult());

    It should_publish_it = () => publisher.Published.ShouldEqual(1);

    It should_retry = () => publisher.Attempts.ShouldEqual(3);

    It should_not_fail = () => exception.ShouldBeNull();

    static FlakyCommandPublisher publisher;
    static Exception exception;
}

[Subject("Bootstrap publishing")]
public class When_a_bootstrap_command_cannot_be_published
{
    Establish context = () =>
    {
        BootstrapPublishing.RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero];
        publisher = new FlakyCommandPublisher(failures: int.MaxValue);
    };

    Because of = () => exception = Catch.Exception(() => publisher.PublishOrThrowAsync(new TestCreateCommand(new TestAggregateId())).GetAwaiter().GetResult());

    It should_fail_the_bootstrap_instead_of_losing_the_command = () => exception.ShouldBeOfExactType<InvalidOperationException>();

    It should_try_four_times = () => publisher.Attempts.ShouldEqual(4);

    static FlakyCommandPublisher publisher;
    static Exception exception;
}

class FlakyCommandPublisher : IPublisher<ICommand>
{
    private int failures;

    public FlakyCommandPublisher(int failures) => this.failures = failures;

    public int Attempts { get; private set; }

    public int Published { get; private set; }

    public Task<bool> PublishAsync(ICommand message, Dictionary<string, string> messageHeaders = null)
    {
        Attempts++;
        if (failures-- > 0)
            return Task.FromResult(false);

        Published++;
        return Task.FromResult(true);
    }

    public Task<bool> PublishAsync(ICommand message, DateTimeOffset publishAt, Dictionary<string, string> messageHeaders = null) => throw new NotSupportedException();

    public Task<bool> PublishAsync(ICommand message, TimeSpan publishAfter, Dictionary<string, string> messageHeaders = null) => throw new NotSupportedException();

    public Task<bool> PublishAsync(byte[] messageRaw, Type messageType, string tenant, Dictionary<string, string> messageHeaders) => throw new NotSupportedException();
}
