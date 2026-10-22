using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Machine.Specifications;

namespace One.Inception.Hosting;

[Subject("Pods start")]
public class When_pods_of_the_same_build_start_together
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        nodes = ClusterNodes.Create(cluster, 5, "pruvit", "shoply");
    };

    Because of = () => Task.WhenAll(nodes.Select(node => node.StartAsync())).GetAwaiter().GetResult();

    It should_run_the_cluster_wide_startups_once_per_tenant = () =>
    {
        nodes.ClusterWideCompleted("pruvit").ShouldEqual(ClusterNode.ClusterWideStartupsCount);
        nodes.ClusterWideCompleted("shoply").ShouldEqual(ClusterNode.ClusterWideStartupsCount);
    };

    It should_run_the_node_startups_on_every_pod = () => nodes.ShouldEachConformTo(node => node.StartCalls >= ClusterNode.NodeStartupsCount);

    It should_start_every_pod = () => nodes.ShouldEachConformTo(node => node.IsStarted);

    It should_remember_the_build = () => cluster.CompletedBy("pruvit").ShouldEqual("build-1");

    It should_never_run_the_cluster_wide_startups_in_parallel = () => cluster.Violations.ShouldBeEmpty();

    static FakeCluster cluster;
    static ClusterNode[] nodes;
}

[Subject("Pods start")]
public class When_a_pod_restarts_with_the_same_build
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        new ClusterNode("node1", cluster, ["pruvit"]).StartAsync().GetAwaiter().GetResult();
        restarted = new ClusterNode("node1-restarted", cluster, ["pruvit"]);
    };

    Because of = () => restarted.StartAsync().GetAwaiter().GetResult();

    It should_not_run_the_cluster_wide_startups_again = () => restarted.ClusterWideStarted("pruvit").ShouldEqual(0);

    It should_run_its_node_startups = () => restarted.StartCalls.ShouldBeGreaterThanOrEqualTo(ClusterNode.NodeStartupsCount);

    static FakeCluster cluster;
    static ClusterNode restarted;
}

[Subject("Pods start")]
public class When_a_new_build_is_deployed
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        oldPods = Enumerable.Range(1, 3).Select(i => new ClusterNode($"old{i}", cluster, ["pruvit"], build: "build-1")).ToArray();
        Task.WhenAll(oldPods.Select(node => node.StartAsync())).GetAwaiter().GetResult();

        newPods = Enumerable.Range(1, 3).Select(i => new ClusterNode($"new{i}", cluster, ["pruvit"], build: "build-2")).ToArray();
    };

    Because of = () =>
    {
        // a rolling deploy: the new pods start one after the other
        foreach (ClusterNode pod in newPods)
            pod.StartAsync().GetAwaiter().GetResult();
    };

    It should_run_the_cluster_wide_startups_once_for_the_new_build = () => newPods.ClusterWideCompleted("pruvit").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_run_them_on_the_first_new_pod = () => newPods[0].ClusterWideCompleted("pruvit").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_remember_the_new_build = () => cluster.CompletedBy("pruvit").ShouldEqual("build-2");

    It should_keep_a_single_key_for_the_tenant = () => cluster.KeysOf("pruvit").ShouldEqual(1);

    static FakeCluster cluster;
    static ClusterNode[] oldPods;
    static ClusterNode[] newPods;
}

[Subject("Pods start")]
public class When_a_pod_of_the_old_build_restarts_during_a_deploy
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        new ClusterNode("old1", cluster, ["pruvit"], build: "build-1").StartAsync().GetAwaiter().GetResult();
        new ClusterNode("new1", cluster, ["pruvit"], build: "build-2").StartAsync().GetAwaiter().GetResult();

        oldRestarted = new ClusterNode("old1-restarted", cluster, ["pruvit"], build: "build-1");
        newStarted = new ClusterNode("new2", cluster, ["pruvit"], build: "build-2");
    };

    Because of = () =>
    {
        oldRestarted.StartAsync().GetAwaiter().GetResult();
        newStarted.StartAsync().GetAwaiter().GetResult();
    };

    It should_run_the_cluster_wide_startups_of_the_old_build = () => oldRestarted.ClusterWideCompleted("pruvit").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_run_the_cluster_wide_startups_of_the_new_build_again_after_it = () => newStarted.ClusterWideCompleted("pruvit").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    It should_end_with_the_new_build = () => cluster.CompletedBy("pruvit").ShouldEqual("build-2");

    static FakeCluster cluster;
    static ClusterNode oldRestarted;
    static ClusterNode newStarted;
}

[Subject("Pods start")]
public class When_a_pod_with_many_tenants_starts
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        tenants = Enumerable.Range(1, 8).Select(i => $"tenant{i}").ToArray();
        node = new ClusterNode("node1", cluster, tenants) { ClusterWideStartupDuration = TimeSpan.FromMilliseconds(300) };
    };

    Because of = () =>
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        node.StartAsync().GetAwaiter().GetResult();
        elapsed = stopwatch.Elapsed;
    };

    It should_bootstrap_every_tenant = () => tenants.ShouldEachConformTo(tenant => node.ClusterWideCompleted(tenant) == ClusterNode.ClusterWideStartupsCount);

    It should_bootstrap_the_tenants_in_parallel = () => elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(300 * ClusterNode.ClusterWideStartupsCount * tenants.Length / 2));

    static FakeCluster cluster;
    static ClusterNode node;
    static string[] tenants;
    static TimeSpan elapsed;
}

[Subject("Pods start")]
public class When_a_pod_starts_and_the_cluster_is_not_reachable
{
    Establish context = () =>
    {
        cluster = new FakeCluster { IsUnreachable = true };
        node = new ClusterNode("node1", cluster, ["pruvit"]);
    };

    Because of = () => exception = Catch.Exception(() => node.StartAsync().GetAwaiter().GetResult());

    It should_start = () => exception.ShouldBeNull();

    It should_run_the_cluster_wide_startups_on_the_pod = () => node.ClusterWideCompleted("pruvit").ShouldEqual(ClusterNode.ClusterWideStartupsCount);

    static FakeCluster cluster;
    static ClusterNode node;
    static Exception exception;
}

[Subject("Pods start")]
public class When_a_pod_starts_and_the_cluster_wide_startups_fail
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        node = new ClusterNode("node1", cluster, ["pruvit"]);
        node.FailClusterWideStartups(1);
    };

    Because of = () => exception = Catch.Exception(() => node.StartAsync().GetAwaiter().GetResult());

    It should_fail_to_start_like_before = () => exception.ShouldNotBeNull();

    It should_not_mark_the_tenant_as_bootstrapped = () => cluster.IsCompleted("pruvit").ShouldBeFalse();

    static FakeCluster cluster;
    static ClusterNode node;
    static Exception exception;
}

[Subject("Booter in a cluster")]
public class When_a_tenant_is_removed_while_its_cluster_wide_startups_run
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

        // the other nodes may have already forgotten the tenant, so this node must not leave a completed mark behind
        node.RemoveTenant("iskambira4");
        node.Unblock();
        new[] { node }.WaitUntilIdle();
    };

    It should_never_mark_the_removed_tenant_as_bootstrapped = () => cluster.Trace("iskambira4").ShouldNotContain(entry => entry.Contains("\"IsCompleted\":true"));

    It should_not_restart_the_node = () => node.Lifetime.StopRequested.ShouldBeFalse();

    It should_not_have_the_tenant = () => node.BootstrappedTenants.ShouldNotContain("iskambira4");

    static FakeCluster cluster;
    static ClusterNode node;
}

[Subject("Pods start")]
public class When_a_pod_skips_the_bootstrap_of_the_tenants
{
    Establish context = () =>
    {
        cluster = new FakeCluster();
        new ClusterNode("node1", cluster, ["pruvit"]).StartAsync().GetAwaiter().GetResult();
        restarted = new ClusterNode("node1-restarted", cluster, ["pruvit"]);
    };

    Because of = () => restarted.StartAsync().GetAwaiter().GetResult();

    It should_skip_the_bootstrap = () => restarted.ClusterWideStarted("pruvit").ShouldEqual(0);

    // The skipped startups used to register the contracts of the projections and indices. A pod which skips them must still find a projection by its contract, for example to build a new projection version.
    It should_find_the_types_by_their_contracts = () => ContractOnlyKnownByItsAttribute.Contract.GetTypeByContract().ShouldEqual(typeof(ContractOnlyKnownByItsAttribute));

    static FakeCluster cluster;
    static ClusterNode restarted;
}

/// <summary>
/// Nothing reads the contract id of this type, so only the registration of the contracts when a pod starts makes it known.
/// </summary>
[DataContract(Name = Contract)]
class ContractOnlyKnownByItsAttribute
{
    public const string Contract = "5d2c7a8e-3f41-4b9c-9e6a-0c1f2b3a4d5e";
}
