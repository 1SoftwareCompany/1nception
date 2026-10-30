using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using One.Inception.Cluster.Job;
using One.Inception.EventStore.Index;
using One.Inception.MessageProcessing;

namespace One.Inception.Hosting;

[Subject("Shutdown deadline")]
public class When_the_host_shutdown_deadline_expires
{
    Because of = () =>
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        applicationServices = new ShutdownConsumer<IApplicationService>();
        indexes = new ShutdownConsumer<IEventStoreIndex>();
        var options = new InceptionHostOptions
        {
            ShutdownTimeout = TimeSpan.FromMilliseconds(500),
            ApplicationServicesEnabled = true,
            ProcessManagersEnabled = false,
            ProjectionsEnabled = false,
            PortsEnabled = false,
            GatewaysEnabled = false,
            TriggersEnabled = false,
            MigrationsEnabled = false,
            NodeBroadcastEnabled = false,
            SystemServicesEnabled = false,
            RpcApiEnabled = false
        };
        var host = new InceptionHost(
            booter: null,
            appServices: applicationServices,
            systemIndices: null,
            indices: indexes,
            projections: new ShutdownConsumer<IProjection>(),
            ports: new ShutdownConsumer<IPort>(),
            processManagers: new ShutdownConsumer<IProcessManager>(),
            gateways: new ShutdownConsumer<IGateway>(),
            triggers: null,
            systemAppServices: null,
            systemProcessManagers: null,
            systemPorts: null,
            systemTriggers: null,
            systemProjections: null,
            migrations: null,
            nodeBroadcasts: null,
            hostOptions: new ShutdownOptions(options),
            serviceProvider: services,
            rpcHost: null,
            jobManager: new JobManager());

        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        host.StopAsync(deadline.Token).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        forwardedToken = applicationServices.Token;
        callerToken = deadline.Token;

        var elapsed = Stopwatch.StartNew();
        host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        defaultTimeout = elapsed.Elapsed;
        defaultDeadlineExpired = applicationServices.Token.IsCancellationRequested && indexes.Token.IsCancellationRequested;
    };

    It should_bound_the_wait_using_the_callers_token = () => forwardedToken.IsCancellationRequested.ShouldBeTrue();
    It should_link_the_callers_token = () => forwardedToken.Equals(callerToken).ShouldBeFalse();
    It should_apply_the_configured_shutdown_deadline = () =>
    {
        defaultDeadlineExpired.ShouldBeTrue();
        (defaultTimeout >= TimeSpan.FromMilliseconds(250) && defaultTimeout < TimeSpan.FromSeconds(5)).ShouldBeTrue();
    };
    It should_also_stop_indexes_when_system_services_are_disabled = () => indexes.Stopped.ShouldBeTrue();

    static CancellationToken callerToken;
    static CancellationToken forwardedToken;
    static bool defaultDeadlineExpired;
    static TimeSpan defaultTimeout;
    static ShutdownConsumer<IApplicationService> applicationServices;
    static ShutdownConsumer<IEventStoreIndex> indexes;
}

public class ShutdownConsumer<T> : IConsumer<T> where T : IMessageHandler
{
    public bool Stopped;
    public CancellationToken Token;
    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Stopped = true;
        Token = cancellationToken;
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }
}

public class ShutdownOptions : IOptionsMonitor<InceptionHostOptions>
{
    public ShutdownOptions(InceptionHostOptions options) { CurrentValue = options; }
    public InceptionHostOptions CurrentValue { get; }
    public InceptionHostOptions Get(string name) => CurrentValue;
    public IDisposable OnChange(Action<InceptionHostOptions, string> listener) => null;
}

[Subject("Shutdown options")]
public class When_shutdown_timeout_is_configured
{
    It should_default_to_15_seconds = () => new InceptionHostOptions().ShutdownTimeout.ShouldEqual(TimeSpan.FromSeconds(15));

    It should_reject_invalid_timer_intervals = () =>
    {
        foreach (TimeSpan value in new[] { Timeout.InfiniteTimeSpan, TimeSpan.Zero, TimeSpan.FromDays(100) })
        {
            bool rejected = false;
            try { new InceptionHostOptions().ShutdownTimeout = value; }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            rejected.ShouldBeTrue();
        }
    };
}

