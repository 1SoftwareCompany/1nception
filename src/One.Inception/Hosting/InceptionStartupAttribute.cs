using System;

namespace One.Inception;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class InceptionStartupAttribute : Attribute
{
    public InceptionStartupAttribute() : this(Bootstraps.Runtime) { }

    public InceptionStartupAttribute(Bootstraps bootstraps)
    {
        Bootstraps = bootstraps;
    }

    public Bootstraps Bootstraps { get; }

    /// <summary>
    /// Set it to true when the startup prepares resources shared by all nodes, such as database tables or system commands.
    /// When a new tenant is added at runtime, only one node in the cluster runs such startup for the tenant. Requires a cluster aware <see cref="Cluster.Job.IInceptionJobRunner"/>, otherwise every node runs it.
    /// </summary>
    public bool ClusterWide { get; set; }
}

public enum Bootstraps
{
    /// <summary>
    /// Bootstraps the environment and prapare it for Inception
    /// </summary>
    Environment = 0,

    /// <summary>
    /// Bootstraps external resources such as database or message broker services
    /// </summary>
    ExternalResource = 10,

    /// <summary>
    /// Bootstraps configuration settings and options for the application
    /// </summary>
    Configuration = 20,

    Aggregates = 30,
    Ports = 40,
    ProcessManagers = 50,
    EventStoreIndices = 55,
    Projections = 60,
    Gateways = 70,

    /// <summary>
    /// Bootstraps anything else. It is executed last and is the default
    /// </summary>
    Runtime = 1000,
}
