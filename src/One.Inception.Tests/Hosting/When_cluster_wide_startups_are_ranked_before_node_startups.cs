using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Machine.Specifications;

namespace One.Inception.Hosting;

/// <summary>
/// Like the event store of a database, which creates the storage of a tenant once for the whole cluster.
/// </summary>
[InceptionStartup(Bootstraps.ExternalResource, ClusterWide = true)]
public class ClusterWideExternalResourceStartup : IInceptionStartup
{
    public Task BootstrapAsync() => Task.CompletedTask;

    public Task BootstrapAsync(IEnumerable<string> tenants) => Task.CompletedTask;
}

[Subject("Booter")]
public class When_a_pod_starts_with_a_cluster_wide_startup_ranked_before_node_startups
{
    Establish context = () => node = new ClusterNode("node1", new FakeCluster(), ["pruvit"]);

    Because of = () =>
    {
        node.StartAsync().GetAwaiter().GetResult();
        calls = node.Calls;
    };

    It should_run_the_cluster_wide_startups_of_the_tenant_in_their_order = () =>
        StartupOrder.ClusterWide(calls, "pruvit").ShouldEqual(StartupOrder.ClusterWideStartups);

    It should_run_the_node_startups_ranked_before_the_last_cluster_wide_startup_first = () =>
        StartupOrder.NodeStartupsRankedBeforeTheLastClusterWide(calls).ShouldEachConformTo(index => index < StartupOrder.FirstClusterWide(calls));

    It should_run_the_node_startups_ranked_after_the_cluster_wide_startups_last = () =>
        StartupOrder.NodeStartupsRankedAfterTheLastClusterWide(calls).ShouldEachConformTo(index => index > StartupOrder.LastClusterWide(calls));

    It should_have_node_startups_ranked_between_the_cluster_wide_startups = () =>
        StartupOrder.NodeStartupsRankedBeforeTheLastClusterWide(calls).ShouldNotBeEmpty();

    static ClusterNode node;
    static IReadOnlyList<ClusterNode.StartupCall> calls;
}

[Subject("Booter")]
public class When_a_tenant_is_added_with_a_cluster_wide_startup_ranked_before_node_startups
{
    Establish context = () =>
    {
        node = new ClusterNode("node1", new FakeCluster(), ["pruvit"]);
        node.StartAsync().GetAwaiter().GetResult();
        callsBefore = node.Calls.Count;
    };

    Because of = () =>
    {
        node.AddTenant("iskambira4");
        new[] { node }.WaitUntilIdle();
        calls = node.Calls.Skip(callsBefore).ToList();
    };

    It should_run_the_cluster_wide_startups_of_the_tenant_in_their_order = () =>
        StartupOrder.ClusterWide(calls, "iskambira4").ShouldEqual(StartupOrder.ClusterWideStartups);

    It should_run_the_node_startups_ranked_before_the_last_cluster_wide_startup_first = () =>
        StartupOrder.NodeStartupsRankedBeforeTheLastClusterWide(calls).ShouldEachConformTo(index => index < StartupOrder.FirstClusterWide(calls));

    It should_run_the_node_startups_ranked_after_the_cluster_wide_startups_last = () =>
        StartupOrder.NodeStartupsRankedAfterTheLastClusterWide(calls).ShouldEachConformTo(index => index > StartupOrder.LastClusterWide(calls));

    static ClusterNode node;
    static int callsBefore;
    static IReadOnlyList<ClusterNode.StartupCall> calls;
}

static class StartupOrder
{
    public static readonly List<Type> ClusterWideStartups = new StartupScanner(new DefaulAssemblyScanner()).Scan().Where(StartupScanner.IsClusterWide).ToList();

    private static readonly Bootstraps LastClusterWideRank = ClusterWideStartups.Max(Rank);

    public static List<Type> ClusterWide(IReadOnlyList<ClusterNode.StartupCall> calls, string tenant) =>
        calls.Where(call => call.IsClusterWide && call.Has(tenant)).Select(call => call.Startup).ToList();

    public static int FirstClusterWide(IReadOnlyList<ClusterNode.StartupCall> calls) => calls.ToList().FindIndex(call => call.IsClusterWide);

    public static int LastClusterWide(IReadOnlyList<ClusterNode.StartupCall> calls) => calls.ToList().FindLastIndex(call => call.IsClusterWide);

    public static IEnumerable<int> NodeStartupsRankedBeforeTheLastClusterWide(IReadOnlyList<ClusterNode.StartupCall> calls) =>
        NodeStartups(calls, rank => rank < LastClusterWideRank);

    public static IEnumerable<int> NodeStartupsRankedAfterTheLastClusterWide(IReadOnlyList<ClusterNode.StartupCall> calls) =>
        NodeStartups(calls, rank => rank > LastClusterWideRank);

    private static IEnumerable<int> NodeStartups(IReadOnlyList<ClusterNode.StartupCall> calls, Func<Bootstraps, bool> ranked) =>
        calls.Select((call, index) => (call, index))
            .Where(c => c.call.IsClusterWide == false && typeof(ITenantStartup).IsAssignableFrom(c.call.Startup) == false && ranked(Rank(c.call.Startup)))
            .Select(c => c.index)
            .ToList();

    private static Bootstraps Rank(Type startup) => startup.GetCustomAttribute<InceptionStartupAttribute>(false)?.Bootstraps ?? Bootstraps.Runtime;
}
