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
/// Runs the cluster wide startups of a tenant on a single node, when the tenant is added and when the nodes start.
/// The completed state is kept in the cluster together with the build which has run the startups, so the other nodes of the same build do not run them again.
/// A node of another build runs them again, because the projections and the indices of its build may be different.
/// </summary>
public sealed class TenantBootstrap_Job : InceptionJob<TenantBootstrap_JobData>
{
    private Func<Task<bool>> bootstrap;
    private string build;
    private bool forget;

    public TenantBootstrap_Job(ILogger<TenantBootstrap_Job> logger) : base(logger) { }

    public override string Name { get; set; } = "tenant-bootstrap";

    /// <summary>
    /// True when this node has run the cluster wide startups.
    /// </summary>
    public bool HasBootstrapped { get; private set; }

    /// <summary>
    /// The exception thrown by the cluster wide startups on this node, if any.
    /// </summary>
    public Exception BootstrapFailure { get; private set; }

    /// <param name="bootstrap">Runs the startups. Returns false when the tenant has been removed meanwhile and the startups have not run or must not be marked as completed.</param>
    internal void Bootstrap(Func<Task<bool>> bootstrap, string build)
    {
        this.bootstrap = bootstrap;
        this.build = build;
    }

    internal void Forget() => forget = true;

    protected override bool DeleteDataWhenCompleted => forget;

    protected override async Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        if (forget)
            return JobExecutionStatus.Completed;

        if (Data.IsCompleted && Data.Build == build)
        {
            logger.LogInformation("The tenant has already been bootstrapped by another node of build {build} at {bootstrappedAt}", build, Data.Timestamp);
            return JobExecutionStatus.Completed;
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
            throw;
        }

        Data.IsCompleted = true;
        Data.Build = build;
        Data.Timestamp = DateTimeOffset.UtcNow;
        Data = await cluster.PingAsync(Data, cancellationToken).ConfigureAwait(false);

        return JobExecutionStatus.Completed;
    }
}

public sealed class TenantBootstrap_JobData : IJobData
{
    public bool IsCompleted { get; set; }

    /// <summary>
    /// The build which has run the cluster wide startups.
    /// </summary>
    public string Build { get; set; }

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

    public TenantBootstrap_Job CreateJob(string tenant, Func<Task<bool>> bootstrap)
    {
        Prepare(tenant);
        job.Bootstrap(bootstrap, build);

        return job;
    }

    /// <summary>
    /// Creates a job which removes the completed state of a removed tenant, so the tenant is bootstrapped again if it is added back.
    /// </summary>
    public TenantBootstrap_Job CreateForgetJob(string tenant)
    {
        Prepare(tenant);
        job.Forget();

        return job;
    }

    private void Prepare(string tenant)
    {
        // The application is part of the name because the applications of a bounded context may run different startups.
        // The build is not part of the name, it is kept in the data, so there is a single key per tenant which is removed with the tenant.
        job.Name = $"urn:{boundedContext.Name}:{tenant}:{job.Name}:{ApplicationName}";
        job.BuildInitialData(() => new TenantBootstrap_JobData() { Timestamp = DateTimeOffset.MinValue }); // the state in the cluster always wins, the build decides if it is completed
    }
}

/// <summary>
/// Identifies the code of the running application: a hash of all assemblies which Inception loads, so a change in any of them, for example in the assembly with the projections, is a new build.
/// The compiler builds the same code into the same assembly, so all nodes running the same image have the same build.
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
