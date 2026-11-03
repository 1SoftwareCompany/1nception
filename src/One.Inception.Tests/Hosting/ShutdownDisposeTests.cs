using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using One.Inception.Cluster.Job;
using One.Inception.EventStore.Index;
using One.Inception.MessageProcessing;

namespace One.Inception.Hosting;

static class ShutdownDisposeHost
{
    public static InceptionHost Create(ShutdownConsumer<IApplicationService> applicationServices)
    {
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

        return new InceptionHost(
            booter: null,
            appServices: applicationServices,
            systemIndices: null,
            indices: new ShutdownConsumer<IEventStoreIndex>(),
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
            serviceProvider: new ServiceCollection().BuildServiceProvider(),
            rpcHost: null,
            jobManager: new JobManager());
    }
}

[Subject("Shutdown deadline")]
public class When_the_host_is_disposed_after_the_stop_deadline_expired
{
    Because of = () =>
    {
        applicationServices = new ShutdownConsumer<IApplicationService>();
        InceptionHost host = ShutdownDisposeHost.Create(applicationServices);
        host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Stopwatch stopwatch = Stopwatch.StartNew();
        host.Dispose();
        elapsed = stopwatch.Elapsed;
    };

    It should_not_stop_again = () => applicationServices.StopCalls.ShouldEqual(1);
    It should_not_wait_for_a_second_deadline = () => elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(250));

    static ShutdownConsumer<IApplicationService> applicationServices;
    static TimeSpan elapsed;
}

[Subject("Shutdown deadline")]
public class When_the_host_is_disposed_without_being_stopped
{
    Because of = () =>
    {
        applicationServices = new ShutdownConsumer<IApplicationService>();
        ShutdownDisposeHost.Create(applicationServices).Dispose();
    };

    It should_still_stop_the_consumers = () => applicationServices.StopCalls.ShouldEqual(1);

    static ShutdownConsumer<IApplicationService> applicationServices;
}

[Subject("Shutdown deadline")]
public class When_the_host_is_disposed_after_it_was_started_again
{
    Because of = () =>
    {
        applicationServices = new ShutdownConsumer<IApplicationService>();
        InceptionHost host = ShutdownDisposeHost.Create(applicationServices);
        host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Catch.Exception(() => host.StartAsync().GetAwaiter().GetResult()); // there is no booter, so it fails, like a start that gets partway
        host.Dispose();
    };

    It should_stop_the_consumers_again = () => applicationServices.StopCalls.ShouldEqual(2);

    static ShutdownConsumer<IApplicationService> applicationServices;
}
