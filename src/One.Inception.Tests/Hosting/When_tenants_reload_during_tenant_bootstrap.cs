using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace One.Inception.Hosting;

[Subject("Booter")]
public class When_tenants_reload_during_tenant_bootstrap
{
    Establish context = () =>
    {
        monitor = new TenantsOptionsMonitorMock("pruvit");
        startup = new SlowStartup();
        new Booter(BooterServices.With(monitor, startup).BuildServiceProvider(), monitor, NullLogger<Booter>.Instance);
    };

    Because of = () =>
    {
        monitor.Change(BooterServices.Tenants("pruvit", "iskambira3"));
        monitor.Change(BooterServices.Tenants("pruvit", "iskambira3"));
        monitor.Change(BooterServices.Tenants("pruvit", "iskambira3"));

        startup.Release.SetResult();
        Task.Delay(500).GetAwaiter().GetResult();
    };

    It should_bootstrap_the_new_tenant_only_once = () =>
        startup.BootstrappedTenants.Count(tenants => tenants.Contains("iskambira3")).ShouldEqual(BooterServices.StartupsCount);

    static TenantsOptionsMonitorMock monitor;
    static SlowStartup startup;

    class SlowStartup : IInceptionStartup, ITenantStartup
    {
        public TaskCompletionSource Release { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentBag<string[]> BootstrappedTenants { get; } = new ConcurrentBag<string[]>();

        public Task BootstrapAsync() => Task.CompletedTask;

        public async Task BootstrapAsync(IEnumerable<string> tenants)
        {
            BootstrappedTenants.Add(tenants.ToArray());
            await Release.Task;
        }
    }
}
