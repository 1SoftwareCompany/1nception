using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception.Cluster.Job;

namespace One.Inception;

/// <summary>
/// Runs the cluster wide startups of a tenant on a single node. The completed state is kept in the cluster with the build, so the other running nodes of the build do not run them again.
/// A starting node runs them again, unless another node has done it after this node started, so a restart repairs the tenant. A failure is kept too, and if they fail again on a starting node it still starts.
/// </summary>
public sealed class TenantBootstrap_Job : InceptionJob<TenantBootstrap_JobData>
{
    private Func<Task<bool>> bootstrap;
    private string build;
    private bool isAdded;
    private DateTimeOffset nodeStartedAt;
    private bool forget;

    public TenantBootstrap_Job(ILogger<TenantBootstrap_Job> logger) : base(logger) { }

    public override string Name { get; set; } = "tenant-bootstrap";

    public bool HasBootstrapped { get; private set; }

    public Exception BootstrapFailure { get; private set; }

    public bool IsFailureKept { get; private set; }

    /// <param name="bootstrap">Returns false when the tenant has been removed meanwhile.</param>
    internal void Bootstrap(Func<Task<bool>> bootstrap, string build, bool isAdded, DateTimeOffset nodeStartedAt)
    {
        this.bootstrap = bootstrap;
        this.build = build;
        this.isAdded = isAdded;
        this.nodeStartedAt = nodeStartedAt;
    }

    internal void Forget() => forget = true;

    protected override bool DeleteDataWhenCompleted => forget;

    protected override async Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        if (forget)
            return JobExecutionStatus.Completed;

        bool hasFailedBefore = Data.IsCompleted && Data.Build == build && Data.Failure is not null;
        if (Data.IsCompleted && Data.Build == build)
        {
            if (Data.Failure is null && (isAdded || Data.Timestamp >= nodeStartedAt))
            {
                logger.LogInformation("The tenant has already been bootstrapped by another node of build {build} at {bootstrappedAt}", build, Data.Timestamp);
                return JobExecutionStatus.Completed;
            }

            if (Data.Failure is not null && isAdded)
            {
                logger.LogWarning("The bootstrap of the tenant has failed on another node of build {build} at {failedAt}: {failure}. It is not run again on this node", build, Data.Timestamp, Data.Failure);
                return JobExecutionStatus.Completed;
            }

            if (Data.Failure is null)
                logger.LogInformation("The tenant was bootstrapped by build {build} at {bootstrappedAt}, before this node started. It is run again because this node starts", build, Data.Timestamp);
            else
                logger.LogInformation("The bootstrap of the tenant has failed on a node of build {build} at {failedAt}: {failure}. It is run again because this node starts", build, Data.Timestamp, Data.Failure);
        }

        try
        {
            if (await bootstrap().ConfigureAwait(false) == false)
            {
                // A removed tenant must not be marked as bootstrapped, its removal may have already been handled by the other nodes and the mark would stay.
                logger.LogInformation("The tenant has been removed while it was being bootstrapped. It is not marked as bootstrapped");
                return JobExecutionStatus.Completed;
            }

            HasBootstrapped = true;
        }
        catch (Exception ex)
        {
            BootstrapFailure = ex;
            if (isAdded == false && hasFailedBefore == false)
                throw;

            IsFailureKept = true;
            await MarkAsCompletedAsync(cluster, failure: ex.Message, cancellationToken).ConfigureAwait(false);
            return JobExecutionStatus.Completed;
        }

        await MarkAsCompletedAsync(cluster, failure: null, cancellationToken).ConfigureAwait(false);
        return JobExecutionStatus.Completed;
    }

    private async Task MarkAsCompletedAsync(IClusterOperations cluster, string failure, CancellationToken cancellationToken)
    {
        Data.IsCompleted = true;
        Data.Build = build;
        Data.Failure = failure;
        Data.Timestamp = DateTimeOffset.UtcNow;
        Data = await cluster.PingAsync(Data, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class TenantBootstrap_JobData : IJobData
{
    public bool IsCompleted { get; set; }

    public string Build { get; set; }

    public string Failure { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TenantBootstrap_JobFactory
{
    private static readonly string ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";

    private readonly TenantBootstrap_Job job;
    private readonly BoundedContext boundedContext;
    private readonly string build;

    public TenantBootstrap_JobFactory(TenantBootstrap_Job job, IOptions<BoundedContext> boundedContext)
        : this(job, boundedContext, ApplicationBuild.Current) { }

    internal TenantBootstrap_JobFactory(TenantBootstrap_Job job, IOptions<BoundedContext> boundedContext, string build)
    {
        this.job = job;
        this.boundedContext = boundedContext.Value;
        this.build = build;
    }

    public TenantBootstrap_Job CreateJob(string tenant, Func<Task<bool>> bootstrap, bool isAdded, DateTimeOffset nodeStartedAt)
    {
        Prepare(tenant);
        job.Bootstrap(bootstrap, build, isAdded, nodeStartedAt);

        return job;
    }

    public TenantBootstrap_Job CreateForgetJob(string tenant)
    {
        Prepare(tenant);
        job.Forget();

        return job;
    }

    private void Prepare(string tenant)
    {
        // The applications of a bounded context may run different startups. The build is kept in the data, so a tenant has a single key.
        job.Name = $"urn:{boundedContext.Name}:{tenant}:{job.Name}:{ApplicationName}";
        job.BuildInitialData(() => new TenantBootstrap_JobData() { Timestamp = DateTimeOffset.MinValue }); // the state in the cluster always wins, the build decides if it is completed
    }
}

/// <summary>
/// A hash of the loaded assemblies. All nodes running the same image have the same build.
/// </summary>
internal static class ApplicationBuild
{
    private static readonly Lazy<string> current = new Lazy<string>(Compute);

    public static string Current => current.Value;

    private static string Compute()
    {
        string modules = string.Join("|", AssemblyLoader.Assemblies.Values
            .Where(assembly => assembly.IsDynamic == false)
            .Select(assembly => $"{assembly.GetName().Name}:{assembly.ManifestModule.ModuleVersionId}")
            .OrderBy(module => module, StringComparer.Ordinal));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modules)), 0, 8).ToLowerInvariant();
    }
}
