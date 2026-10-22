using System;
using System.Linq;
using System.Threading.Tasks;
using Machine.Specifications;

namespace One.Inception.Hosting;

[Subject("Booter in a cluster")]
public class When_a_tenant_is_added_on_many_nodes
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 10, "pruvit");
        foreach (var node in nodes) node.Block();
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => nodes.ClusterWideStarted("iskambira4") > 0);
        Task.Delay(200).GetAwaiter().GetResult(); // the other nodes find the job running and wait

        foreach (var node in nodes) node.Unblock();
        nodes.WaitUntilIdle();
    };

    It should_run_the_cluster_wide_startups_on_one_node_only = () => nodes.ClusterWideStarted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_complete_the_cluster_wide_startups = () => nodes.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_run_the_node_startups_on_every_node = () => nodes.ShouldEachConformTo(node => node.NodeCompleted("iskambira4") == ClusterNode.NodeStartupsCount);

    It should_bootstrap_the_tenant_on_every_node = () => nodes.ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4"));

    It should_keep_the_completed_bootstrap_in_the_cluster = () => cluster.IsCompleted("iskambira4").ShouldBeTrue();

    It should_not_restart_any_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Booter in a cluster")]
public class When_the_node_bootstrapping_a_tenant_is_killed
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
        foreach (var node in nodes) node.Block();
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => nodes.ClusterWideStarted("iskambira4") > 0);

        killed = nodes.Single(node => node.ClusterWideStarted("iskambira4") > 0);
        killed.Kill();
        foreach (var node in nodes.Except([killed])) node.Unblock();

        nodes.WaitUntilIdle();
    };

    It should_let_another_node_bootstrap_the_tenant = () => nodes.Except([killed]).ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_bootstrap_the_tenant_on_every_live_node = () => nodes.Except([killed]).ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4") && node.NodeCompleted("iskambira4") == ClusterNode.NodeStartupsCount);

    It should_keep_the_completed_bootstrap_in_the_cluster = () => cluster.IsCompleted("iskambira4").ShouldBeTrue();

    It should_not_restart_any_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static ClusterNode killed;
}

[Subject("Booter in a cluster")]
public class When_the_bootstrap_fails_on_the_nodes
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
        foreach (var node in nodes) node.FailClusterWideStartups(times: 1);
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        nodes.WaitUntilIdle();
    };

    It should_complete_the_cluster_wide_startups_once = () => nodes.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_bootstrap_the_tenant_on_every_node = () => nodes.ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4") && node.NodeCompleted("iskambira4") >= ClusterNode.NodeStartupsCount);

    It should_keep_the_completed_bootstrap_in_the_cluster = () => cluster.IsCompleted("iskambira4").ShouldBeTrue();

    It should_not_restart_any_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Booter in a cluster")]
public class When_the_bootstrap_keeps_failing_on_the_nodes
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
        foreach (var node in nodes) node.FailClusterWideStartups(times: int.MaxValue);
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => nodes.All(node => node.Lifetime.StopRequested));
    };

    It should_restart_every_node = () => nodes.ShouldEachConformTo(node => node.Lifetime.StopRequested);

    It should_not_mark_the_tenant_as_bootstrapped = () => cluster.IsCompleted("iskambira4").ShouldBeFalse();

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Booter in a cluster")]
public class When_the_cluster_is_not_reachable
{
    Establish context = () =>
    {
        cluster = new FakeCluster { IsUnreachable = true };
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        nodes.WaitUntilIdle();
    };

    It should_bootstrap_the_tenant_on_every_node = () => nodes.ShouldEachConformTo(node =>
        node.BootstrappedTenants.Contains("iskambira4") &&
        node.ClusterWideCompleted("iskambira4") == ClusterNode.ClusterWideStartupsCount &&
        node.NodeCompleted("iskambira4") == ClusterNode.NodeStartupsCount);

    It should_not_restart_any_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Booter in a cluster")]
public class When_the_cluster_rejects_writes
{
    Establish context = () =>
    {
        cluster = new FakeCluster { RejectsWrites = true };
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        nodes.WaitUntilIdle();
    };

    It should_bootstrap_the_tenant_on_every_node = () => nodes.ShouldEachConformTo(node =>
        node.BootstrappedTenants.Contains("iskambira4") &&
        node.ClusterWideCompleted("iskambira4") == ClusterNode.ClusterWideStartupsCount &&
        node.NodeCompleted("iskambira4") == ClusterNode.NodeStartupsCount);

    It should_not_restart_any_node = () => nodes.AnyStopRequested().ShouldBeFalse();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Booter in a cluster")]
public class When_the_cluster_says_the_bootstrap_is_running_for_too_long
{
    Establish context = () => node = new ClusterNode("node1", new FakeCluster(), ["pruvit"], ClusterNode.ClusterBehavior.AlwaysRunning, clusterBootstrapTimeoutMs: 300);

    Because of = () =>
    {
        node.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => node.IsIdle);
    };

    It should_run_the_cluster_wide_startups_on_this_node = () => node.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_run_the_node_startups = () => node.NodeCompleted("iskambira4").ShouldEqual(ClusterNode.NodeStartupsCount);

    It should_bootstrap_the_tenant = () => node.BootstrappedTenants.ShouldContain("iskambira4");

    static ClusterNode node;
}

[Subject("Booter in a cluster")]
public class When_there_is_no_cluster_job_runner
{
    Establish context = () => node = new ClusterNode("node1", new FakeCluster(), ["pruvit"], ClusterNode.ClusterBehavior.None);

    Because of = () =>
    {
        node.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => node.IsIdle);
    };

    It should_run_the_cluster_wide_startups_on_this_node = () => node.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_bootstrap_the_tenant = () => node.BootstrappedTenants.ShouldContain("iskambira4");

    static ClusterNode node;
}

[Subject("Booter in a cluster")]
public class When_a_node_notices_the_new_tenant_late
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        early = new ClusterNode("early", cluster, ["pruvit"]);
        late = new ClusterNode("late", cluster, ["pruvit"]);
    };

    Because of = () =>
    {
        early.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => early.IsIdle);

        late.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => late.IsIdle);
    };

    It should_not_run_the_cluster_wide_startups_again = () => late.ClusterWideStarted("iskambira4").ShouldEqual(0);

    It should_run_the_node_startups = () => late.NodeCompleted("iskambira4").ShouldEqual(ClusterNode.NodeStartupsCount);

    It should_bootstrap_the_tenant = () => late.BootstrappedTenants.ShouldContain("iskambira4");

    static FakeCluster cluster;
    static ClusterNode early;
    static ClusterNode late;
}

[Subject("Booter in a cluster")]
public class When_a_node_of_another_build_notices_the_new_tenant
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        early = new ClusterNode("early", cluster, ["pruvit"], build: "build-1");
        late = new ClusterNode("late", cluster, ["pruvit"], build: "build-2");
    };

    Because of = () =>
    {
        early.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => early.IsIdle);

        late.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => late.IsIdle);
    };

    It should_run_the_cluster_wide_startups_again_because_its_projections_may_be_different = () => late.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_remember_its_build = () => cluster.CompletedBy("iskambira4").ShouldEqual("build-2");

    It should_keep_a_single_key_for_the_tenant = () => cluster.KeysOf("iskambira4").ShouldEqual(1);

    It should_bootstrap_the_tenant = () => late.BootstrappedTenants.ShouldContain("iskambira4");

    static FakeCluster cluster;
    static ClusterNode early;
    static ClusterNode late;
}

[Subject("Booter in a cluster")]
public class When_several_tenants_are_added_at_once
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");
    };

    Because of = () =>
    {
        foreach (var node in nodes) node.SetTenants("pruvit", "iskambira4", "iskambira5");
        nodes.WaitUntilIdle();
    };

    It should_run_the_cluster_wide_startups_once_per_tenant = () =>
    {
        nodes.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);
        nodes.ClusterWideCompleted("iskambira5").ShouldEqual(ClusterNode.ClusterWideStartupsCount);
    };

    It should_bootstrap_the_tenants_on_every_node = () => nodes.ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4") && node.BootstrappedTenants.Contains("iskambira5"));

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Booter in a cluster")]
public class When_a_removed_tenant_is_added_back
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 3, "pruvit");

        nodes.AddTenant("iskambira4");
        nodes.WaitUntilIdle();
        callsBeforeRemove = nodes.Sum(node => node.AllCalls);
    };

    Because of = () =>
    {
        nodes.RemoveTenant("iskambira4");
        nodes.WaitUntilIdle();
        callsAfterRemove = nodes.Sum(node => node.AllCalls);
        forgotten = cluster.Contains("iskambira4") == false;

        nodes.AddTenant("iskambira4");
        nodes.WaitUntilIdle();
    };

    It should_not_bootstrap_anything_when_a_tenant_is_only_removed = () => callsAfterRemove.ShouldEqual(callsBeforeRemove);

    It should_forget_the_completed_bootstrap_when_the_tenant_is_removed = () => forgotten.ShouldBeTrue();

    It should_run_the_cluster_wide_startups_again = () => nodes.ClusterWideCompleted("iskambira4").ShouldEqual(2 * ClusterNode.ClusterWideStartupsCount);

    It should_bootstrap_the_tenant_on_every_node = () => nodes.ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4") && node.NodeCompleted("iskambira4") == 2 * ClusterNode.NodeStartupsCount);

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static int callsBeforeRemove;
    static int callsAfterRemove;
    static bool forgotten;
}

[Subject("Booter in a cluster")]
public class When_a_tenant_is_removed_while_another_node_bootstraps_it
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        bootstrapping = ClusterNodes.Create(cluster, 1, "pruvit");
        foreach (var node in bootstrapping) node.Block();
        removing = new ClusterNode("removing", cluster, ["pruvit", "iskambira4"]);
    };

    Because of = () =>
    {
        bootstrapping.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => bootstrapping.ClusterWideStarted("iskambira4") > 0);

        removing.RemoveTenant("iskambira4");
        Task.Delay(200).GetAwaiter().GetResult(); // the removing node finds the job running and waits

        foreach (var node in bootstrapping) node.Unblock();
        bootstrapping.Append(removing).WaitUntilIdle();
    };

    It should_forget_the_completed_bootstrap_after_the_other_node_finishes = () => cluster.Contains("iskambira4").ShouldBeFalse();

    It should_bootstrap_the_tenant_on_the_other_nodes = () => bootstrapping.ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4"));

    It should_remove_the_tenant_from_the_removing_node = () => removing.BootstrappedTenants.ShouldNotContain("iskambira4");

    static FakeCluster cluster;
    static ClusterNode[] bootstrapping;
    static ClusterNode removing;
}

[Subject("Booter in a cluster")]
public class When_a_tenant_is_removed_and_added_back_while_the_node_is_busy
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        node = new ClusterNode("node1", cluster, ["pruvit", "iskambira4"]);
        node.Block();
    };

    Because of = () =>
    {
        node.AddTenant("iskambira5");
        BooterServices.WaitUntil(() => node.ClusterWideStarted("iskambira5") > 0);

        node.RemoveTenant("iskambira4");
        node.AddTenant("iskambira4");

        node.Unblock();
        BooterServices.WaitUntil(() => node.IsIdle);
    };

    It should_bootstrap_the_added_back_tenant_again = () =>
    {
        node.ClusterWideCompleted("iskambira4").ShouldEqual(ClusterNode.ClusterWideStartupsCount);
        node.NodeCompleted("iskambira4").ShouldEqual(ClusterNode.NodeStartupsCount);
    };

    It should_bootstrap_both_tenants = () => node.BootstrappedTenants.ShouldContain("iskambira4", "iskambira5");

    It should_not_restart = () => node.Lifetime.StopRequested.ShouldBeFalse();

    static FakeCluster cluster;
    static ClusterNode node;
}

[Subject("Booter in a cluster")]
public class When_a_tenant_is_added_and_removed_while_the_node_is_busy
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        node = new ClusterNode("node1", cluster, ["pruvit"]);
        node.Block();
    };

    Because of = () =>
    {
        node.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => node.ClusterWideStarted("iskambira4") > 0);

        node.AddTenant("iskambira5");
        node.RemoveTenant("iskambira5");

        node.Unblock();
        BooterServices.WaitUntil(() => node.IsIdle);
    };

    It should_not_bootstrap_the_removed_tenant = () => node.ClusterWideStarted("iskambira5").ShouldEqual(0);

    It should_end_with_the_current_tenants = () => node.BootstrappedTenants.ShouldContainOnly("pruvit", "iskambira4");

    It should_not_restart = () => node.Lifetime.StopRequested.ShouldBeFalse();

    static FakeCluster cluster;
    static ClusterNode node;
}

[Subject("Booter in a cluster")]
public class When_a_pod_starts_while_a_tenant_is_being_added
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 2, "pruvit");
        foreach (var node in nodes) node.Block();
    };

    Because of = () =>
    {
        nodes.AddTenant("iskambira4");
        BooterServices.WaitUntil(() => nodes.ClusterWideStarted("iskambira4") > 0);

        // the pod waits for the node which is bootstrapping the tenant, so it starts after it is unblocked
        started = new ClusterNode("started", cluster, ["pruvit", "iskambira4"]);
        Task starting = started.StartAsync();

        foreach (var node in nodes) node.Unblock();
        starting.GetAwaiter().GetResult();
        nodes.Append(started).WaitUntilIdle();

        // the tenant is removed and added back after the pod has started
        all = nodes.Append(started).ToArray();
        all.RemoveTenant("iskambira4");
        all.WaitUntilIdle();
        all.AddTenant("iskambira4");
        all.WaitUntilIdle();
    };

    It should_bootstrap_all_tenants_on_the_started_pod = () => started.StartCalls.ShouldBeGreaterThan(0);

    It should_bootstrap_the_tenant_on_every_node = () => all.ShouldEachConformTo(node => node.BootstrappedTenants.Contains("iskambira4"));

    It should_run_the_cluster_wide_startups_once_per_add = () => all.ClusterWideCompleted("iskambira4").ShouldEqual(2 * ClusterNode.ClusterWideStartupsCount);

    It should_not_restart_any_node = () => all.AnyStopRequested().ShouldBeFalse();

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
    static ClusterNode started;
    static ClusterNode[] all;
}
