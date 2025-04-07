using Machine.Specifications;
using System.Collections.Generic;
using System.Linq;
using System;

namespace One.Inception.Migrations;

[Subject("Migration")]
public class When_inception_startups_are_executed
{
    Establish context = () =>
    {
        scanner = new StartupScanner(new TestAssemblyScanner());
    };

    Because of = () => startupTypes = scanner.Scan().ToList();

    It should_find_all_bootstraps = () => startupTypes.Count.ShouldEqual(11);

    It should_order_the_startup_types = () =>
    {
        for (int i = 0; i < expectedOrderedType.Count; i++)
        {
            startupTypes[i].ShouldEqual(expectedOrderedType[i]);
        }
    };

    static StartupScanner scanner;
    static List<Type> startupTypes;
    static List<Type> expectedOrderedType = new List<Type>()
    {
        typeof(TestAssemblyScanner.EnvironmentStartup),
        typeof(TestAssemblyScanner.ExternalResourceStartup),
        typeof(TestAssemblyScanner.ConfigurationStartup),
        typeof(TestAssemblyScanner.AggregatesStartup),
        typeof(TestAssemblyScanner.PortsStartup),
        typeof(TestAssemblyScanner.SagasStartup),
        typeof(TestAssemblyScanner.SecondProjectionsStartup),
        typeof(TestAssemblyScanner.ProjectionsStartup),
        typeof(TestAssemblyScanner.GatewaysStartup),
        typeof(TestAssemblyScanner.NoAttributeStartup),
        typeof(TestAssemblyScanner.RuntimeStartup)
    };
}

public class TestAssemblyScanner : IAssemblyScanner
{
    public IEnumerable<Type> Scan()
    {
        yield return typeof(SagasStartup);
        yield return typeof(GatewaysStartup);
        yield return typeof(SecondProjectionsStartup);
        yield return typeof(AggregatesStartup);
        yield return typeof(ExternalResourceStartup);
        yield return typeof(ProjectionsStartup);
        yield return typeof(NoAttributeStartup);
        yield return typeof(ConfigurationStartup);
        yield return typeof(EnvironmentStartup);
        yield return typeof(RuntimeStartup);
        yield return typeof(PortsStartup);
    }

    [InceptionStartup(Bootstraps.Environment)] public class EnvironmentStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.ExternalResource)] public class ExternalResourceStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Configuration)] public class ConfigurationStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Aggregates)] public class AggregatesStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Ports)] public class PortsStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Sagas)] public class SagasStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Projections)] public class ProjectionsStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Projections)] public class SecondProjectionsStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Gateways)] public class GatewaysStartup : IInceptionStartup { public void Bootstrap() { } }
    [InceptionStartup(Bootstraps.Runtime)] public class RuntimeStartup : IInceptionStartup { public void Bootstrap() { } }
    public class NoAttributeStartup : IInceptionStartup { public void Bootstrap() { } }
}
