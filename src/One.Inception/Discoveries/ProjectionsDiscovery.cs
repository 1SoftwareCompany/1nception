﻿using System.Collections.Generic;
using System.Linq;
using One.Inception.Projections;
using One.Inception.Projections.Rebuilding;
using One.Inception.Projections.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Discoveries;

public class ProjectionsDiscovery : HandlersDiscovery<IProjection>
{
    protected override IEnumerable<DiscoveredModel> DiscoverHandlers(DiscoveryContext context)
    {
        var options = new InceptionHostOptions();
        context.Configuration.GetSection("Inception").Bind(options);

        IEnumerable<DiscoveredModel> models =
            base.DiscoverHandlers(context)
            .Concat(GetSupportingModels())
            .Concat(GetModels());

        var hasProjectionStore = context.FindServiceExcept<IProjectionStore>(typeof(MissingProjections)).Any();
        if (hasProjectionStore == false)
        {
            models = models.Concat(RegisterMissingModels());
        }

        return models;
    }

    IEnumerable<DiscoveredModel> GetModels()
    {
        yield return new DiscoveredModel(typeof(IProjectionReader), typeof(ProjectionRepository), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IProjectionWriter), typeof(ProjectionRepository), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionRepository), typeof(ProjectionRepository), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionRepositoryWithFallback<>), typeof(ProjectionRepositoryWithFallback<>), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionRepositoryWithFallback<,>), typeof(ProjectionRepositoryWithFallback<,>), ServiceLifetime.Transient);
    }

    IEnumerable<DiscoveredModel> RegisterMissingModels()
    {
        yield return new DiscoveredModel(typeof(IProjectionStore), typeof(MissingProjections), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IInitializableProjectionStore), typeof(MissingProjections), ServiceLifetime.Transient);
    }

    IEnumerable<DiscoveredModel> GetSupportingModels()
    {
        yield return new DiscoveredModel(typeof(IProjectionVersioningPolicy), typeof(MarkupInterfaceProjectionVersioningPolicy), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(MarkupInterfaceProjectionVersioningPolicy), typeof(MarkupInterfaceProjectionVersioningPolicy), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(ProjectionHasher), typeof(ProjectionHasher), ServiceLifetime.Singleton);

        yield return new DiscoveredModel(typeof(LatestProjectionVersionFinder), typeof(LatestProjectionVersionFinder), ServiceLifetime.Transient);

        yield return new DiscoveredModel(typeof(ProjectionFinderViaReflection), typeof(ProjectionFinderViaReflection), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(ProjectionBootstrapper), typeof(ProjectionBootstrapper), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IProjectionVersionFinder), typeof(ProjectionFinderViaReflection), ServiceLifetime.Transient) { CanAddMultiple = true };
    }
}

public class SystemdProjectionsDiscovery : HandlersDiscovery<ISystemProjection>
{
    protected override IEnumerable<DiscoveredModel> DiscoverHandlers(DiscoveryContext context)
    {
        IEnumerable<DiscoveredModel> models =
            base.DiscoverHandlers(context)
            .Concat(GetSupportingModels())
            .Concat(GetModels());

        var hasProjectionStore = context.FindServiceExcept<IProjectionStore>(typeof(MissingProjections)).Any();
        if (hasProjectionStore == false)
        {
            models = models.Concat(RegisterMissingModels());
        }
        return models;
    }

    IEnumerable<DiscoveredModel> GetModels()
    {
        yield return new DiscoveredModel(typeof(IProjectionReader), typeof(ProjectionRepository), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IProjectionWriter), typeof(ProjectionRepository), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionRepository), typeof(ProjectionRepository), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionRepositoryWithFallback<>), typeof(ProjectionRepositoryWithFallback<>), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionRepositoryWithFallback<,>), typeof(ProjectionRepositoryWithFallback<,>), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProjectionVersionHelper), typeof(ProjectionVersionHelper), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(ProgressTracker), typeof(ProgressTracker), ServiceLifetime.Scoped);
    }

    IEnumerable<DiscoveredModel> GetSupportingModels()
    {
        yield return new DiscoveredModel(typeof(IProjectionVersioningPolicy), typeof(MarkupInterfaceProjectionVersioningPolicy), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(MarkupInterfaceProjectionVersioningPolicy), typeof(MarkupInterfaceProjectionVersioningPolicy), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(ProjectionHasher), typeof(ProjectionHasher), ServiceLifetime.Singleton);
    }

    IEnumerable<DiscoveredModel> RegisterMissingModels()
    {
        yield return new DiscoveredModel(typeof(IProjectionStore), typeof(MissingProjections), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IInitializableProjectionStore), typeof(MissingProjections), ServiceLifetime.Transient);

    }
}
