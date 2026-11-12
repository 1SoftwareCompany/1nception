using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace One.Inception.Hosting;

[Subject("Booter")]
public class When_new_tenant_bootstrap_fails
{
    Establish context = () => booter = new FailingBootstrap(failures: 1);

    Because of = () => booter.ChangeTenants("pruvit", "iskambira3");

    It should_try_it_once = () => booter.Startup.Failed.ShouldEqual(1);

    It should_log_the_failure = () => booter.Log.Errors.ShouldContain(entry => entry.Contains("Failed to bootstrap new tenants") && entry.Contains("iskambira3"));

    It should_keep_the_service_running = () => booter.Lifetime.StopRequested.ShouldBeFalse();

    static FailingBootstrap booter;
}

[Subject("Booter")]
public class When_another_tenant_is_added_after_a_failed_bootstrap
{
    Establish context = () =>
    {
        booter = new FailingBootstrap(failures: 1);
        booter.ChangeTenants("pruvit", "iskambira3");
    };

    Because of = () => booter.ChangeTenants("pruvit", "iskambira3", "iskambira4");

    It should_bootstrap_the_added_tenant = () => booter.Startup.Tenants.ShouldContain("iskambira4");

    It should_not_try_the_failed_tenant_again = () => booter.Startup.Tenants.ShouldNotContain("iskambira3");

    static FailingBootstrap booter;
}

class FailingBootstrap
{
    private readonly TenantsOptionsMonitorMock monitor = new TenantsOptionsMonitorMock("pruvit");
    private readonly Booter booter;

    public FailingBootstrap(int failures)
    {
        Startup = new FailingStartup(failures);

        ServiceCollection services = BooterServices.With(monitor, Startup);
        services.AddSingleton<IHostApplicationLifetime>(Lifetime);
        booter = new Booter(services.BuildServiceProvider(), monitor, Log);
    }

    public FailingStartup Startup { get; }

    public ApplicationLifetimeStub Lifetime { get; } = new ApplicationLifetimeStub();

    public ListLogger<Booter> Log { get; } = new ListLogger<Booter>();

    public void ChangeTenants(params string[] tenants)
    {
        monitor.Change(BooterServices.Tenants(tenants));
        BooterServices.WaitUntil(() => booter.IsIdle);
    }
}

class FailingStartup : IInceptionStartup, ITenantStartup
{
    private int failuresLeft;
    private readonly List<string> tenants = new List<string>();

    public FailingStartup(int failures) => failuresLeft = failures;

    public int Failed;

    public int Succeeded;

    public IReadOnlyList<string> Tenants { get { lock (tenants) return tenants.ToArray(); } }

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
        lock (this.tenants) this.tenants.AddRange(tenants);
        return Task.CompletedTask;
    }
}

class ApplicationLifetimeStub : IHostApplicationLifetime
{
    public bool StopRequested { get; private set; }

    public CancellationToken ApplicationStarted => CancellationToken.None;

    private readonly CancellationTokenSource stopping = new CancellationTokenSource();

    public CancellationToken ApplicationStopping => stopping.Token;

    public void Stop() => stopping.Cancel();

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => StopRequested = true;
}
