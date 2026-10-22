using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;

namespace One.Inception.Hosting;

/// <summary>
/// Tenants are added and removed while pods are killed and started, without waiting for the nodes between the changes.
/// Every node receives every tenants change, like they all watch the same configuration.
/// </summary>
[Subject("Booter in a cluster")]
public class When_pods_and_tenants_change_randomly
{
    Because of = () =>
    {
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("BOOTER_CHAOS_SEEDS"), out int count) ? count : 8; // raise it locally to stress the booter
        int parallel = int.TryParse(Environment.GetEnvironmentVariable("BOOTER_CHAOS_PARALLEL"), out int degree) ? degree : Environment.ProcessorCount;

        // The seeds are independent clusters, so they run in parallel. Each seed blocks a thread while it waits, so the pool gets enough threads up front instead of growing slowly.
        ThreadPool.GetMinThreads(out int workers, out int completionPorts);
        ThreadPool.SetMinThreads(Math.Max(workers, parallel * 8), completionPorts);

        var results = new List<string>[seeds];
        Parallel.For(1, seeds + 1, new ParallelOptions { MaxDegreeOfParallelism = parallel }, seed =>
            results[seed - 1] = Run(seed).Select(problem => $"seed {seed}: {problem}").ToList());

        problems.AddRange(results.SelectMany(result => result));
    };

    It should_bootstrap_every_tenant_without_issues = () => problems.ShouldBeEmpty();

    static List<string> Run(int seed)
    {
        var random = new Random(seed);
        var cluster = new FakeCluster();
        var tenantsPool = new[] { "iskambira1", "iskambira2", "iskambira3", "iskambira4" };
        var config = new List<string> { "pruvit" };
        var lastAddStep = new Dictionary<string, int>();
        var nodes = new List<ClusterNode>();
        int nodeNumber = 0;

        ClusterNode NewNode()
        {
            // a killed pod is considered gone when it has not written a new tenant within 300ms, instead of 60s in production
            var node = new ClusterNode($"node{++nodeNumber}", cluster, config.ToArray(), acknowledgementTimeoutMs: 300) { ClusterWideStartupDuration = TimeSpan.FromMilliseconds(random.Next(5, 30)) };
            nodes.Add(node);
            return node;
        }

        IEnumerable<ClusterNode> Live() => nodes.Where(node => node.IsDead == false);

        for (int i = 0; i < 4; i++)
            NewNode().StartAsync().GetAwaiter().GetResult();

        for (int i = 0; i < 30; i++)
        {
            int step = cluster.NextStep();
            int operation = random.Next(100);
            string[] absent = tenantsPool.Except(config).ToArray();
            string[] present = tenantsPool.Intersect(config).ToArray();

            if (operation < 40 && absent.Length > 0)
            {
                string tenant = absent[random.Next(absent.Length)];
                config.Add(tenant);
                lastAddStep[tenant] = step;
            }
            else if (operation < 70 && present.Length > 0)
            {
                config.Remove(present[random.Next(present.Length)]);
            }
            else if (operation < 85 && Live().Count() > 2)
            {
                ClusterNode[] live = Live().ToArray();
                live[random.Next(live.Length)].Kill();
                continue;
            }
            else if (Live().Count() < 6)
            {
                NewNode().StartAsync().GetAwaiter().GetResult();
                continue;
            }

            foreach (ClusterNode node in Live().OrderBy(_ => random.Next()))
                node.SetTenants(config.ToArray());

            Task.Delay(random.Next(0, 40)).GetAwaiter().GetResult();
        }

        Live().WaitUntilIdle();
        nodes.StopAll();

        var problems = new List<string>();
        if (Live().AreIdle() == false)
            problems.Add("the nodes did not finish");

        problems.AddRange(cluster.Violations);

        foreach (ClusterNode node in nodes.Where(node => node.Lifetime.StopRequested))
            problems.Add($"{node.Name} requested a restart: {string.Join(" | ", node.Log.Entries.Where(entry => entry.StartsWith("Warning") || entry.StartsWith("Critical")).Distinct().Take(3))}");

        foreach (ClusterNode node in Live())
        {
            if (node.BootstrappedTenants.OrderBy(t => t).SequenceEqual(config.OrderBy(t => t)) == false)
                problems.Add($"{node.Name} has bootstrapped [{string.Join(", ", node.BootstrappedTenants)}] instead of [{string.Join(", ", config)}]");
        }

        foreach (var added in lastAddStep.Where(added => config.Contains(added.Key)))
        {
            bool bootstrappedAfterTheAdd = nodes.Any(node => node.ClusterWideCompletedSince(added.Key, added.Value));
            if (bootstrappedAfterTheAdd == false)
                problems.Add($"the cluster wide startups of {added.Key} did not run after it was added at step {added.Value}. Trace: {string.Join(" | ", cluster.Trace(added.Key).TakeLast(12))}");
        }

        return problems;
    }

    static List<string> problems = new List<string>();
}
