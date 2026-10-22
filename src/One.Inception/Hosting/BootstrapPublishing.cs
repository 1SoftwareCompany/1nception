using System;
using System.Threading.Tasks;

namespace One.Inception;

/// <summary>
/// Publishes the commands of the bootstrap startups. A single node publishes them for the whole cluster and the tenant is then marked as bootstrapped,
/// so a command which cannot be published must fail the bootstrap instead of being lost: the bootstrap is retried and the tenant is not marked.
/// </summary>
internal static class BootstrapPublishing
{
    internal static TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2)];

    public static async Task PublishOrThrowAsync(this IPublisher<ICommand> publisher, ICommand command)
    {
        for (int attempt = 0; ; attempt++)
        {
            if (await publisher.PublishAsync(command).ConfigureAwait(false))
                return;

            if (attempt >= RetryDelays.Length)
                throw new InvalidOperationException($"Unable to publish {command.GetType().Name} while bootstrapping after {RetryDelays.Length + 1} attempts.");

            await Task.Delay(RetryDelays[attempt]).ConfigureAwait(false);
        }
    }
}
