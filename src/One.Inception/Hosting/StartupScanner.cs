using System;
using System.Linq;
using System.Collections.Generic;

namespace One.Inception;

public class StartupScanner
{
    private readonly IAssemblyScanner assemblyScanner;

    public StartupScanner(IAssemblyScanner assemblyScanner)
    {
        this.assemblyScanner = assemblyScanner;
    }

    public IEnumerable<Type> Scan()
    {
        var startups = assemblyScanner
            .Scan()
            .Where(type => type.IsAbstract == false && type.IsClass && typeof(IInceptionStartup).IsAssignableFrom(type))
            .OrderBy(type => GetStartupRank(type));

        return startups;
    }

    public IEnumerable<Type> ScanForTenantStartups()
    {
        var startups = assemblyScanner
            .Scan()
            .Where(type => type.IsAbstract == false && type.IsClass && typeof(ITenantStartup).IsAssignableFrom(type))
            .OrderBy(type => GetStartupRank(type));

        return startups;
    }

    public IEnumerable<Type> ScanForClusterWideStartups()
    {
        return Scan().Where(IsClusterWide);
    }

    /// <summary>
    /// They run before the cluster wide startups, because those publish messages which need them, such as the transport.
    /// </summary>
    public IEnumerable<Type> ScanForNodeStartupsBeforeClusterWide()
    {
        int lastClusterWideRank = GetLastClusterWideRank();

        return Scan().Where(type => IsClusterWide(type) == false && GetStartupRank(type) <= lastClusterWideRank);
    }

    public IEnumerable<Type> ScanForNodeStartupsAfterClusterWide()
    {
        int lastClusterWideRank = GetLastClusterWideRank();

        return Scan().Where(type => IsClusterWide(type) == false && GetStartupRank(type) > lastClusterWideRank);
    }

    private int GetLastClusterWideRank()
    {
        return ScanForClusterWideStartups()
            .Select(GetStartupRank)
            .DefaultIfEmpty(-1)
            .Max();
    }

    private int GetStartupRank(Type type)
    {
        InceptionStartupAttribute startupAttribute = type
            .GetCustomAttributes(typeof(InceptionStartupAttribute), false)
            .SingleOrDefault() as InceptionStartupAttribute;

        Bootstraps rank = Bootstraps.Runtime;

        if (startupAttribute is null == false)
            rank = startupAttribute.Bootstraps;

        return (int)rank;
    }

    public static bool IsClusterWide(Type type)
    {
        InceptionStartupAttribute startupAttribute = type
            .GetCustomAttributes(typeof(InceptionStartupAttribute), false)
            .SingleOrDefault() as InceptionStartupAttribute;

        return startupAttribute?.ClusterWide == true;
    }
}
