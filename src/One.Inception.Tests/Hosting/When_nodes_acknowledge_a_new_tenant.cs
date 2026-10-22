using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Machine.Specifications;

namespace One.Inception.Hosting;

[Subject("Tenants acknowledgement")]
public class When_a_node_has_not_reloaded_the_new_tenant_yet
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
        foreach (ClusterNode node in nodes)
            node.StartAsync().GetAwaiter().GetResult();
    };

    Because of = () =>
    {
        nodes[0].AddTenant("iskambira4");
        nodes[1].AddTenant("iskambira4");
        Task.Delay(300).GetAwaiter().GetResult();
        startedBeforeTheSlowNodeKnewTheTenant = nodes.ClusterWideStarted("iskambira4");

        nodes[2].AddTenant("iskambira4");
        nodes.WaitUntilIdle();
    };

    It should_not_bootstrap_the_tenant_until_every_node_knows_it = () => startedBeforeTheSlowNodeKnewTheTenant.ShouldEqual(0);

    It should_bootstrap_it_once_every_node_knows_it = () => nodes.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_start_the_bootstrap_when_all_nodes_knew_the_tenant = () =>
        cluster.NodesWhenBootstrapStarted("iskambira4").ShouldEachConformTo(node => node.KnewTheTenant);

    It should_know_all_the_nodes = () => cluster.NodesWhenBootstrapStarted("iskambira4").Count.ShouldEqual(3);

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static int startedBeforeTheSlowNodeKnewTheTenant;
}

[Subject("Tenants acknowledgement")]
public class When_all_nodes_get_the_new_tenant_at_once
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 10, "pruvit");
        foreach (ClusterNode node in nodes)
            node.StartAsync().GetAwaiter().GetResult();
    };

    Because of = () =>
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        nodes.AddTenant("iskambira4");
        nodes.WaitUntilIdle();
        elapsed = stopwatch.Elapsed;
    };

    It should_bootstrap_the_tenant_once = () => nodes.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_start_the_bootstrap_when_all_nodes_knew_the_tenant = () =>
        cluster.NodesWhenBootstrapStarted("iskambira4").ShouldEachConformTo(node => node.KnewTheTenant);

    It should_not_wait_for_the_timeout = () => elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1.5));


    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static TimeSpan elapsed;
}

[Subject("Tenants acknowledgement")]
public class When_a_node_never_gets_the_new_tenant
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = new[]
        {
            new ClusterNode("node1", cluster, ["pruvit"], acknowledgementTimeoutMs: 300),
            new ClusterNode("stale", cluster, ["pruvit"], acknowledgementTimeoutMs: 300)
        };
        foreach (ClusterNode node in nodes)
            node.StartAsync().GetAwaiter().GetResult();
    };

    Because of = () =>
    {
        nodes[0].AddTenant("iskambira4");
        nodes.WaitUntilIdle();
    };

    It should_bootstrap_the_tenant_anyway = () => nodes.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_say_which_node_does_not_know_it = () =>
        nodes[0].Log.Entries.ShouldContain(entry => entry.StartsWith("Warning") && entry.Contains("stale") && entry.Contains("iskambira4"));

    It should_not_restart_the_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Tenants acknowledgement")]
public class When_a_node_crashes_and_tenants_are_added
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = Enumerable.Range(1, 3).Select(i => new ClusterNode($"node{i}", cluster, ["pruvit"], acknowledgementTimeoutMs: 500)).ToArray();
        foreach (ClusterNode node in nodes)
            node.StartAsync().GetAwaiter().GetResult();

        // a crashed pod cannot remove itself, so it is still in the nodes
        nodes[2].Kill();
    };

    Because of = () =>
    {
        live = nodes.Take(2).ToArray();

        Stopwatch stopwatch = Stopwatch.StartNew();
        live.AddTenant("iskambira4");
        live.WaitUntilIdle();
        firstAdd = stopwatch.Elapsed;

        stopwatch.Restart();
        live.AddTenant("iskambira5");
        live.WaitUntilIdle();
        secondAdd = stopwatch.Elapsed;
    };

    It should_bootstrap_the_first_tenant_after_waiting_for_the_crashed_node = () =>
    {
        live.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);
        firstAdd.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(500));
    };

    It should_say_which_node_is_considered_gone = () =>
        live.SelectMany(node => node.Log.Entries).ShouldContain(entry => entry.Contains("node3") && entry.Contains("iskambira4") && entry.Contains("considered gone"));

    It should_remove_the_crashed_node = () => cluster.NodesWhichKnow("pruvit").ShouldContainOnly("node1", "node2");

    It should_not_wait_for_it_again = () => secondAdd.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));

    It should_bootstrap_the_second_tenant = () => live.ClusterWideCompleted("iskambira5").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static ClusterNode[] live;
    static TimeSpan firstAdd;
    static TimeSpan secondAdd;
}

[Subject("Tenants acknowledgement")]
public class When_a_slow_node_was_considered_gone_and_gets_the_next_tenant
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        fast = new ClusterNode("fast", cluster, ["pruvit"], acknowledgementTimeoutMs: 300);
        slow = new ClusterNode("slow", cluster, ["pruvit"], acknowledgementTimeoutMs: 300);
        fast.StartAsync().GetAwaiter().GetResult();
        slow.StartAsync().GetAwaiter().GetResult();

        // the slow node does not get the tenant in time, so it is removed from the nodes
        fast.AddTenant("iskambira4");
        new[] { fast }.WaitUntilIdle();
    };

    Because of = () =>
    {
        slow.AddTenant("iskambira4");
        new[] { slow }.WaitUntilIdle();
    };

    It should_be_back_in_the_nodes = () => cluster.NodesWhichKnow("iskambira4").ShouldContainOnly("fast", "slow");

    static FakeCluster cluster;
    static ClusterNode fast;
    static ClusterNode slow;
}

[Subject("Tenants acknowledgement")]
public class When_a_node_is_shut_down
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = Enumerable.Range(1, 2)
            .Select(i => new ClusterNode($"node{i}", cluster, ["pruvit"], acknowledgementTimeoutMs: 10_000))
            .ToArray();
        foreach (ClusterNode node in nodes)
            node.StartAsync().GetAwaiter().GetResult();

        nodes[1].Stop();
        BooterServices.WaitUntil(() => cluster.NodesWhichKnow("pruvit").Contains("node2") == false);
    };

    Because of = () =>
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        nodes[0].AddTenant("iskambira4");
        new[] { nodes[0] }.WaitUntilIdle();
        elapsed = stopwatch.Elapsed;
    };

    It should_remove_itself_from_the_nodes = () => cluster.NodesWhichKnow("pruvit").ShouldNotContain("node2");

    It should_not_be_waited_for = () => elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));

    It should_bootstrap_the_tenant = () => nodes[0].ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    Cleanup after = () => nodes.StopAll();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static TimeSpan elapsed;
}

[Subject("Tenants acknowledgement")]
public class When_the_new_tenant_is_removed_while_waiting_for_the_nodes
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = new[]
        {
            new ClusterNode("node1", cluster, ["pruvit"], acknowledgementTimeoutMs: 10_000),
            new ClusterNode("node2", cluster, ["pruvit"], acknowledgementTimeoutMs: 10_000)
        };
        foreach (ClusterNode node in nodes)
            node.StartAsync().GetAwaiter().GetResult();
    };

    Because of = () =>
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        nodes[0].AddTenant("iskambira4");
        Task.Delay(200).GetAwaiter().GetResult();
        nodes[0].RemoveTenant("iskambira4");
        nodes.WaitUntilIdle();
        elapsed = stopwatch.Elapsed;
    };

    It should_stop_waiting = () => elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));

    It should_not_restart_the_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    It should_not_have_the_tenant = () => nodes[0].BootstrappedTenants.ShouldNotContain("iskambira4");

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static TimeSpan elapsed;
}

[Subject("Tenants acknowledgement")]
public class When_a_node_starts
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        node = new ClusterNode("node1", cluster, ["pruvit", "shoply"]);
    };

    Because of = () => node.StartAsync().GetAwaiter().GetResult();

    It should_tell_the_other_nodes_its_tenants_before_it_handles_messages = () =>
    {
        cluster.NodesWhichKnow("pruvit").ShouldContainOnly("node1");
        cluster.NodesWhichKnow("shoply").ShouldContainOnly("node1");
    };

    static FakeCluster cluster;
    static ClusterNode node;
}
