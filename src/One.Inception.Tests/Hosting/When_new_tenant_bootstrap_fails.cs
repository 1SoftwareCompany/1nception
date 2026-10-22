using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace One.Inception.Hosting;

[Subject("Booter")]
public class When_new_tenant_bootstrap_fails_temporarily
{
    Establish context = () =>
    {
        monitor = new TenantsOptionsMonitorMock("pruvit");
        startup = new FailingStartup(failures: 2);
        lifetime = new ApplicationLifetimeStub();
        booter = FailingBootstrap.NewBooter(monitor, startup, lifetime);
    };

    Because of = () =>
    {
        monitor.Change(BooterServices.Tenants("pruvit", "iskambira3"));
        BooterServices.WaitUntil(() => startup.Succeeded == BooterServices.StartupsCount);
    };

    It should_retry_until_the_bootstrap_succeeds = () => startup.Succeeded.ShouldEqual(BooterServices.StartupsCount);

    It should_not_stop_the_application = () => lifetime.StopRequested.ShouldBeFalse();

    static TenantsOptionsMonitorMock monitor;
    static FailingStartup startup;
    static ApplicationLifetimeStub lifetime;
    static Booter booter;
}

[Subject("Booter")]
public class When_new_tenant_bootstrap_keeps_failing
{
    Establish context = () =>
    {
        monitor = new TenantsOptionsMonitorMock("pruvit");
        startup = new FailingStartup(failures: int.MaxValue);
        lifetime = new ApplicationLifetimeStub();
        booter = FailingBootstrap.NewBooter(monitor, startup, lifetime);
    };

    Because of = () =>
    {
        monitor.Change(BooterServices.Tenants("pruvit", "iskambira3"));
        BooterServices.WaitUntil(() => lifetime.StopRequested);
    };

    It should_try_once_and_retry_three_times = () => startup.Failed.ShouldEqual(4);

    It should_stop_the_application_so_it_restarts = () => lifetime.StopRequested.ShouldBeTrue();

    static TenantsOptionsMonitorMock monitor;
    static FailingStartup startup;
    static ApplicationLifetimeStub lifetime;
    static Booter booter;
}

static class FailingBootstrap
{
    public static Booter NewBooter(TenantsOptionsMonitorMock monitor, FailingStartup startup, ApplicationLifetimeStub lifetime)
    {
        ServiceCollection services = BooterServices.With(monitor, startup);
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        return new Booter(services.BuildServiceProvider(), monitor, NullLogger<Booter>.Instance, [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]);
    }
}

class FailingStartup : IInceptionStartup, ITenantStartup
{
    private int failuresLeft;

    public FailingStartup(int failures) => failuresLeft = failures;

    public int Failed;

    public int Succeeded;

    public Task BootstrapAsync() => Task.CompletedTask;

    public Task BootstrapAsync(IEnumerable<string> tenants)
    {
        if (failuresLeft > 0)
        {
            failuresLeft--;
            Interlocked.Increment(ref Failed);
            throw new InvalidOperationException("RabbitMQ is not reachable");
        }

        Interlocked.Increment(ref Succeeded);
        return Task.CompletedTask;
    }
}

class ApplicationLifetimeStub : IHostApplicationLifetime
{
    public bool StopRequested { get; private set; }

    public CancellationToken ApplicationStarted => CancellationToken.None;

    private readonly CancellationTokenSource stopping = new CancellationTokenSource();

    public CancellationToken ApplicationStopping => stopping.Token;

    /// <summary>
    /// The host is stopping, like when the pod is shut down.
    /// </summary>
    public void Stop() => stopping.Cancel();

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => StopRequested = true;
}
