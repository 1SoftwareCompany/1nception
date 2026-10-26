using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.Cluster;
using One.Inception.Cluster.Job;
using One.Inception.Hosting;

namespace One.Inception;

/// <summary>
/// A node (pod) of a service. All the startups are replaced with recorders and the cluster job runner works with a <see cref="FakeCluster"/>.
/// </summary>
class ClusterNode
{
    private static readonly List<Type> Startups = new StartupScanner(new DefaulAssemblyScanner()).Scan().ToList();

    public static int ClusterWideStartupsCount => Startups.Count(StartupScanner.IsClusterWide);

    public static int NodeStartupsCount => Startups.Count(type => StartupScanner.IsClusterWide(type) == false);

    private readonly FakeCluster cluster;
    private readonly TenantsOptionsMonitorMock monitor;
    private readonly ConcurrentQueue<StartupCall> calls = new ConcurrentQueue<StartupCall>();
    private readonly Booter booter;
    private TaskCompletionSource gate;
    private int clusterWideFailuresLeft;
    private string clusterWideFailingTenant;

    /// <param name="build">The build of the code which the node runs. The nodes of a deploy have the same build.</param>
    public ClusterNode(string name, FakeCluster cluster, string[] tenants, ClusterBehavior clusterBehavior = ClusterBehavior.Normal, int clusterBootstrapTimeoutMs = 5000,
        int acknowledgementTimeoutMs = 2000, string build = "build-1")
    {
        Name = name;
        this.cluster = cluster;
        monitor = new TenantsOptionsMonitorMock(tenants);

        var scanner = new StartupScanner(new DefaulAssemblyScanner());

        ServiceCollection services = BooterServices.With(monitor, null);
        foreach (Type startupType in scanner.Scan().Concat(scanner.ScanForTenantStartups()))
            services.AddSingleton(startupType, _ => new RecordingStartup(startupType, this));

        services.AddSingleton<IOptions<BoundedContext>>(Options.Create(new BoundedContext { Name = "tests" }));
        services.AddTransient<TenantBootstrap_Job>();
        services.AddTransient(sp => new TenantBootstrap_JobFactory(sp.GetRequiredService<TenantBootstrap_Job>(), sp.GetRequiredService<IOptions<BoundedContext>>(), build));
        services.AddTransient<IInceptionJobRunner>(_ => clusterBehavior switch
        {
            ClusterBehavior.AlwaysRunning => new AlwaysRunningJobRunner(),
            ClusterBehavior.None => null,
            _ => new FakeClusterJobRunner(cluster, name)
        });
        if (clusterBehavior != ClusterBehavior.None)
            services.AddSingleton<ITenantsRegistry>(new FakeTenantsRegistry(cluster, name));
        services.AddSingleton<IHostApplicationLifetime>(Lifetime);

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<MessageProcessing.DefaultContextFactory>(); // in the host it exists before the booter

        var acknowledgement = new TenantsAcknowledgement(serviceProvider, monitor, Log, name, TimeSpan.FromMilliseconds(acknowledgementTimeoutMs), TimeSpan.FromMilliseconds(20));

        booter = new Booter(serviceProvider, monitor, Log, new ClusterWideBootstrap(serviceProvider, monitor, Log, acknowledgement, pollInterval: TimeSpan.FromMilliseconds(20), timeout: TimeSpan.FromMilliseconds(clusterBootstrapTimeoutMs)));
    }

    public string Name { get; }

    public ListLogger<Booter> Log { get; } = new ListLogger<Booter>();

    public bool IsDead { get; private set; }

    public ApplicationLifetimeStub Lifetime { get; } = new ApplicationLifetimeStub();

    public bool IsIdle => booter.IsIdle;

    public IEnumerable<string> BootstrappedTenants => booter.BootstrappedTenants;

    public IEnumerable<string> Tenants => monitor.CurrentValue.Tenants;

    public int StartCalls => calls.Count(call => call.IsStart);

    public bool IsStarted { get; private set; }

    public int ClusterWideStarted(string tenant) => calls.Count(call => call.IsStart == false && call.IsClusterWide && call.Has(tenant));

    public int ClusterWideCompleted(string tenant) => calls.Count(call => call.IsStart == false && call.IsClusterWide && call.Has(tenant) && call.Completed);

    /// <summary>
    /// True when the cluster wide startups of the tenant have completed on this node at or after the step, either for the new tenant or when the pod started.
    /// </summary>
    public bool ClusterWideCompletedSince(string tenant, int step) => calls.Any(call => call.IsClusterWide && call.Has(tenant) && call.Completed && call.Step >= step);

    public int NodeCompleted(string tenant) => calls.Count(call => call.IsStart == false && call.IsClusterWide == false && call.Has(tenant) && call.Completed);

    public int AllCalls => calls.Count;

    public IReadOnlyList<StartupCall> Calls => calls.ToArray();

    public void Block() => gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Unblock() => gate?.TrySetResult();

    public void FailClusterWideStartups(int times, string tenant = null)
    {
        clusterWideFailuresLeft = times;
        clusterWideFailingTenant = tenant;
    }

    public async Task StartAsync()
    {
        await booter.BootstrapInceptionAsync().ConfigureAwait(false);
        IsStarted = true;
    }

    public void AddTenant(string tenant) => SetTenants(Tenants.Append(tenant).ToArray());

    public void RemoveTenant(string tenant) => SetTenants(Tenants.Where(t => t != tenant).ToArray());

    public void SetTenants(params string[] tenants)
    {
        if (IsDead == false)
            monitor.Change(BooterServices.Tenants(tenants));
    }

    public void Kill()
    {
        IsDead = true;
        cluster.Kill(Name); // a killed pod does not run its stopping callbacks
    }

    public void Stop()
    {
        if (IsDead == false) // a killed pod is gone, it cannot be stopped
            Lifetime.Stop();
    }

    public IEnumerable<string> NodesWhichKnow(string tenant) => cluster.NodesWhichKnow(tenant);

    public TimeSpan ClusterWideStartupDuration { get; set; } = TimeSpan.Zero;

    private async Task BootstrapAsync(Type startupType, string[] tenants)
    {
        if (IsDead)
            await Task.Delay(Timeout.Infinite).ConfigureAwait(false); // a killed pod does nothing

        var call = new StartupCall(startupType, tenants);
        calls.Enqueue(call);

        if (call.IsClusterWide)
        {
            foreach (string tenant in tenants)
                cluster.EnterClusterWideStartup(tenant, Name);

            try
            {
                if ((clusterWideFailingTenant is null || tenants.Contains(clusterWideFailingTenant)) && Interlocked.Decrement(ref clusterWideFailuresLeft) >= 0)
                    throw new InvalidOperationException("Cassandra is not reachable");

                if (ClusterWideStartupDuration > TimeSpan.Zero)
                    await Task.Delay(ClusterWideStartupDuration).ConfigureAwait(false);

                if (gate is not null)
                    await gate.Task.ConfigureAwait(false);
            }
            finally
            {
                foreach (string tenant in tenants)
                    cluster.ExitClusterWideStartup(tenant, Name);
            }
        }

        if (IsDead == false)
        {
            call.Completed = true;
            call.Step = cluster.Step;
        }
    }

    public enum ClusterBehavior
    {
        Normal,
        AlwaysRunning,
        None
    }

    public class StartupCall
    {
        public StartupCall(Type startup, string[] tenants, bool isStart = false)
        {
            Startup = startup;
            Tenants = tenants;
            IsClusterWide = StartupScanner.IsClusterWide(startup);
            IsStart = isStart;
        }

        public Type Startup { get; }

        public string[] Tenants { get; }

        public bool IsClusterWide { get; }

        public volatile bool Completed;

        public int Step;

        public bool IsStart { get; }

        public bool Has(string tenant) => Tenants?.Contains(tenant) == true;
    }

    class RecordingStartup : IInceptionStartup, ITenantStartup
    {
        private readonly Type startupType;
        private readonly ClusterNode node;

        public RecordingStartup(Type startupType, ClusterNode node)
        {
            this.startupType = startupType;
            this.node = node;
        }

        public Task BootstrapAsync()
        {
            node.calls.Enqueue(new StartupCall(startupType, node.Tenants.ToArray(), isStart: true) { Completed = true, Step = node.cluster.Step });
            return Task.CompletedTask;
        }

        public Task BootstrapAsync(IEnumerable<string> tenants) => node.BootstrapAsync(startupType, tenants.ToArray());
    }
}

/// <summary>
/// Keeps the job keys like the Consul KV store: a key is held by one session at a time and keeps its value after it is released.
/// It also checks that the cluster wide startups of a tenant never run on two live nodes at the same time.
/// </summary>
class FakeCluster
{
    private readonly object sync = new object();
    private readonly Dictionary<string, (string Session, string Json)> keys = new Dictionary<string, (string, string)>();
    private readonly Dictionary<string, HashSet<string>> runningClusterWideStartups = new Dictionary<string, HashSet<string>>();
    private readonly HashSet<string> deadNodes = new HashSet<string>();
    private readonly List<string> violations = new List<string>();
    private int step;

    public volatile bool IsUnreachable;

    /// <summary>
    /// The nodes can read from the cluster but every write fails.
    /// </summary>
    public volatile bool RejectsWrites;

    public int Step => Volatile.Read(ref step);

    public int NextStep() => Interlocked.Increment(ref step);

    public IReadOnlyList<string> Violations { get { lock (sync) return violations.ToList(); } }

    public bool Contains(string tenant) => Read(keys => keys.Keys.Any(key => IsKeyOf(key, tenant)));

    private readonly Dictionary<string, List<string>> registeredTenants = new Dictionary<string, List<string>>();

    public IEnumerable<string> NodesWhichKnow(string tenant) => Read(_ => registeredTenants.Where(node => node.Value.Contains(tenant)).Select(node => node.Key).ToList());

    public bool Register(string writer, string node, IEnumerable<string> tenants) => Write(_ =>
    {
        if (deadNodes.Contains(writer))
            return false;

        registeredTenants[node] = tenants.ToList();
        return true;
    });

    public bool Unregister(string writer, string node) => Write(_ => deadNodes.Contains(writer) == false && registeredTenants.Remove(node));

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> RegisteredTenants() =>
        Read(_ => registeredTenants.ToDictionary(node => node.Key, node => (IReadOnlyCollection<string>)node.Value.ToList()));

    public List<(string Node, bool KnewTheTenant)> NodesWhenBootstrapStarted(string tenant) => Read(_ => bootstrapStarts.TryGetValue(tenant, out var nodes) ? nodes.ToList() : new List<(string, bool)>());

    private readonly Dictionary<string, List<(string Node, bool KnewTheTenant)>> bootstrapStarts = new Dictionary<string, List<(string, bool)>>();

    public bool IsCompleted(string tenant) => Read(keys => keys.Any(key => IsKeyOf(key.Key, tenant) && JsonSerializer.Deserialize<TenantBootstrap_JobData>(key.Value.Json).IsCompleted));

    public string CompletedBy(string tenant) => Read(keys => keys.Where(key => IsKeyOf(key.Key, tenant)).Select(key => JsonSerializer.Deserialize<TenantBootstrap_JobData>(key.Value.Json)).Where(data => data.IsCompleted).Select(data => data.Build).SingleOrDefault());

    public string FailureOf(string tenant) => Read(keys => keys.Where(key => IsKeyOf(key.Key, tenant)).Select(key => JsonSerializer.Deserialize<TenantBootstrap_JobData>(key.Value.Json)).Where(data => data.IsCompleted).Select(data => data.Failure).SingleOrDefault());

    public int KeysOf(string tenant) => Read(keys => keys.Keys.Count(key => IsKeyOf(key, tenant)));

    public bool IsHeld(string key) => Read(keys => keys.TryGetValue(key, out var value) && value.Session is not null);

    /// <summary>
    /// The sessions of a killed node expire, so the keys it holds are released.
    /// </summary>
    public void Kill(string node) => Write(keys =>
    {
        deadNodes.Add(node);
        foreach (var running in runningClusterWideStartups.Values)
            running.Remove(node);

        foreach (string key in keys.Keys.Where(key => keys[key].Session?.StartsWith($"{node}/") == true).ToList())
            keys[key] = (null, keys[key].Json);
        return true;
    });

    private readonly List<(int Step, string Session, string Op, string Key, string Json)> trace = new List<(int, string, string, string, string)>();

    public IEnumerable<string> Trace(string tenant) => Read(_ => trace.Where(entry => entry.Key.Contains($":{tenant}:")).Select(entry => $"[{entry.Step}] {entry.Session?.Split('/')[0]} {entry.Op}: {entry.Json}").ToList());

    private void Record(string op, string key, string session, string json) => trace.Add((step, session, op, key, json));

    public bool TryAcquire(string key, string session, string json) => Write(keys =>
    {
        if (IsSessionDead(session) || keys.TryGetValue(key, out var value) && value.Session is not null)
            return false;

        keys[key] = (session, json);
        if (key.Contains("tenant-bootstrap")) Record("acquire", key, session, json);
        return true;
    });

    public void Release(string key, string session) => Write(keys =>
    {
        if (keys.TryGetValue(key, out var value) && value.Session == session)
            keys[key] = (null, value.Json);
        return true;
    });

    public string Get(string key) => Read(keys => keys.TryGetValue(key, out var value) ? value.Json : null);

    public bool Put(string key, string session, string json) => Write(keys =>
    {
        if (keys.TryGetValue(key, out var value) == false || value.Session != session)
            return false;

        keys[key] = (session, json);
        if (key.Contains("tenant-bootstrap")) Record("put", key, session, json);
        return true;
    });

    public bool Delete(string key, string session) => Write(keys =>
    {
        bool deleted = keys.TryGetValue(key, out var value) && value.Session == session && keys.Remove(key);
        Record(deleted ? "delete" : "delete-failed", key, session, "");
        return deleted;
    });

    public void EnterClusterWideStartup(string tenant, string node) => Write(_ =>
    {
        if (deadNodes.Contains(node))
            return false;

        if (runningClusterWideStartups.TryGetValue(tenant, out HashSet<string> running) == false)
            runningClusterWideStartups[tenant] = running = new HashSet<string>();

        if (running.Any(other => other != node))
            violations.Add($"The cluster wide startups of `{tenant}` run on `{node}` and `{string.Join(", ", running)}` at the same time");

        if (registeredTenants.Count > 0 && bootstrapStarts.ContainsKey(tenant) == false)
            bootstrapStarts[tenant] = registeredTenants.Select(known => (known.Key, known.Value.Contains(tenant))).ToList();

        running.Add(node);
        return true;
    });

    public void ExitClusterWideStartup(string tenant, string node) => Write(_ =>
    {
        if (runningClusterWideStartups.TryGetValue(tenant, out HashSet<string> running))
            running.Remove(node);
        return true;
    });

    private bool IsSessionDead(string session) => deadNodes.Contains(session.Split('/')[0]);

    private static bool IsKeyOf(string key, string tenant) => key.Contains($":{tenant}:");

    private T Read<T>(Func<Dictionary<string, (string Session, string Json)>, T> read) { lock (sync) return read(keys); }

    private T Write<T>(Func<Dictionary<string, (string Session, string Json)>, T> write) { lock (sync) return write(keys); }
}

/// <summary>
/// Runs a job like the Consul job runner: only the node which acquires the job key runs the job, the others get <see cref="JobExecutionStatus.Running"/>.
/// </summary>
class FakeClusterJobRunner : IInceptionJobRunner, IClusterOperations
{
    private readonly FakeCluster cluster;
    private readonly string session;
    private string jobName;

    public FakeClusterJobRunner(FakeCluster cluster, string node)
    {
        this.cluster = cluster;
        session = $"{node}/{Guid.NewGuid()}";
    }

    public JobManager JobManager => null;

    public async Task<JobExecutionStatus> ExecuteAsync(IInceptionJob<object> job, CancellationToken cancellationToken = default)
    {
        jobName = job.Name;

        try
        {
            ThrowIfUnreachable();
            if (cluster.IsHeld(jobName))
                return JobExecutionStatus.Running;

            await job.SyncInitialStateAsync(this, cancellationToken).ConfigureAwait(false);
            ThrowIfRejectsWrites();
            if (cluster.TryAcquire(jobName, session, JsonSerializer.Serialize(job.Data, job.Data.GetType())) == false)
                return JobExecutionStatus.Running;
        }
        catch (Exception)
        {
            return JobExecutionStatus.Failed; // the Consul job runner logs and returns Failed
        }

        try
        {
            return await job.RunAsync(this, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            cluster.Release(jobName, session);
        }
    }

    public Task<TData> PingAsync<TData>(CancellationToken cancellationToken = default) where TData : class, new()
    {
        ThrowIfUnreachable();
        string json = cluster.Get(jobName);
        return Task.FromResult(json is null ? null : JsonSerializer.Deserialize<TData>(json));
    }

    public Task<TData> PingAsync<TData>(TData data, CancellationToken cancellationToken = default) where TData : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfUnreachable();
        ThrowIfRejectsWrites();
        cluster.Put(jobName, session, JsonSerializer.Serialize(data));
        return Task.FromResult(data);
    }

    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfUnreachable();
        ThrowIfRejectsWrites();
        return Task.FromResult(cluster.Delete(jobName, session));
    }

    public void Dispose()
    {
        if (jobName is not null)
            cluster.Release(jobName, session); // like the Consul job runner, which releases the key when it is disposed
    }

    private void ThrowIfUnreachable()
    {
        if (cluster.IsUnreachable)
            throw new HttpRequestException("Consul is not reachable");
    }

    private void ThrowIfRejectsWrites()
    {
        if (cluster.RejectsWrites)
            throw new HttpRequestException("Consul rejected the write");
    }
}

class FakeTenantsRegistry : ITenantsRegistry
{
    private readonly FakeCluster cluster;
    private readonly string writer;

    public FakeTenantsRegistry(FakeCluster cluster, string writer)
    {
        this.cluster = cluster;
        this.writer = writer;
    }

    public Task RegisterAsync(string node, IEnumerable<string> tenants)
    {
        ThrowIfUnavailable(write: true);
        cluster.Register(writer, node, tenants);
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(string node)
    {
        ThrowIfUnavailable(write: true);
        cluster.Unregister(writer, node);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetAllAsync()
    {
        ThrowIfUnavailable(write: false);
        return Task.FromResult(cluster.RegisteredTenants());
    }

    private void ThrowIfUnavailable(bool write)
    {
        if (cluster.IsUnreachable)
            throw new HttpRequestException("Consul is not reachable");

        if (write && cluster.RejectsWrites)
            throw new HttpRequestException("Consul rejected the write");
    }
}

/// <summary>
/// Behaves like a cluster where another node holds the job forever, or like the Consul job runner when Consul answers with errors.
/// </summary>
class AlwaysRunningJobRunner : IInceptionJobRunner
{
    public JobManager JobManager => null;

    public Task<JobExecutionStatus> ExecuteAsync(IInceptionJob<object> job, CancellationToken cancellationToken = default) => Task.FromResult(JobExecutionStatus.Running);

    public void Dispose() { }
}

static class ClusterNodes
{
    public static ClusterNode[] Create(FakeCluster cluster, int count, params string[] tenants) =>
        Enumerable.Range(1, count).Select(i => new ClusterNode($"node{i}", cluster, tenants)).ToArray();

    public static void AddTenant(this IEnumerable<ClusterNode> nodes, string tenant)
    {
        foreach (ClusterNode node in nodes)
            node.AddTenant(tenant);
    }

    public static void RemoveTenant(this IEnumerable<ClusterNode> nodes, string tenant)
    {
        foreach (ClusterNode node in nodes)
            node.RemoveTenant(tenant);
    }

    public static bool AreIdle(this IEnumerable<ClusterNode> nodes) => nodes.Where(node => node.IsDead == false).All(node => node.IsIdle);

    public static void WaitUntilIdle(this IEnumerable<ClusterNode> nodes) => BooterServices.WaitUntil(() => nodes.AreIdle());

    public static int ClusterWideCompleted(this IEnumerable<ClusterNode> nodes, string tenant) => nodes.Sum(node => node.ClusterWideCompleted(tenant));

    public static int ClusterWideStarted(this IEnumerable<ClusterNode> nodes, string tenant) => nodes.Sum(node => node.ClusterWideStarted(tenant));

    public static bool AnyStopRequested(this IEnumerable<ClusterNode> nodes) => nodes.Any(node => node.Lifetime.StopRequested);

    public static void StopAll(this IEnumerable<ClusterNode> nodes)
    {
        foreach (ClusterNode node in nodes)
            node.Stop();
    }
}

class ListLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> entries = new ConcurrentQueue<string>();

    public IEnumerable<string> Entries => entries;

    public IEnumerable<string> Errors => entries.Where(entry => entry.StartsWith(nameof(LogLevel.Error)) || entry.StartsWith(nameof(LogLevel.Critical)));

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        entries.Enqueue($"{logLevel}: {formatter(state, exception)}{(exception is null ? string.Empty : $" => {exception.GetType().Name}: {exception.Message}")}");
    }
}
