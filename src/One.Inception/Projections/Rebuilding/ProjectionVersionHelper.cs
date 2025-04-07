﻿using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using One.Inception.EventStore.Index.Handlers;
using One.Inception.Projections.Versioning;
using One.Inception.MessageProcessing;
using One.Inception.EventStore.Index;
using System.Threading.Tasks;

namespace One.Inception.Projections.Rebuilding;

public class ProjectionVersionHelper
{
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly ProjectionHasher projectionHasher;
    private readonly IInitializableProjectionStore projectionVersionInitializer;
    private readonly IProjectionReader projectionReader;
    private readonly ILogger<ProjectionVersionHelper> logger;

    public ProjectionVersionHelper(IInceptionContextAccessor contextAccessor, IProjectionReader projectionReader, IInitializableProjectionStore projectionVersionInitializer, ProjectionHasher projectionHasher, ILogger<ProjectionVersionHelper> logger)
    {
        this.contextAccessor = contextAccessor;
        this.projectionReader = projectionReader;
        this.projectionVersionInitializer = projectionVersionInitializer;
        this.projectionHasher = projectionHasher;
        this.logger = logger;
    }

    /// <summary>
    /// Initializing new projection version if needed
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public void InitializeNewProjectionVersion()
    {
        ProjectionVersion newPersistentVersion = GetNewProjectionVersion();
        projectionVersionInitializer.InitializeAsync(newPersistentVersion);
    }

    public async Task<bool> ShouldBeRetriedAsync(ProjectionVersion version)
    {
        bool isVersionTrackerMissing = await IsVersionTrackerMissingAsync().ConfigureAwait(false);
        if (isVersionTrackerMissing)
        {
            InitializeNewProjectionVersion();

            if (version.ProjectionName.Equals(ProjectionVersionsHandler.ContractId, StringComparison.OrdinalIgnoreCase) == false)
                return true;
        }

        IndexStatus indexStatus = await GetIndexStatusAsync<EventToAggregateRootId>().ConfigureAwait(false);
        Type projectionType = version.ProjectionName.GetTypeByContract();

        if (isVersionTrackerMissing && IsNotSystemProjection(projectionType)) return true;
        if (indexStatus.IsNotPresent() && IsNotSystemProjection(projectionType)) return true;

        return false;
    }

    public async Task<bool> ShouldBeCanceledAsync(ProjectionVersion version, DateTimeOffset dueDate)
    {
        if (HasReplayTimeout(dueDate))
        {
            logger.LogError("Rebuild of projection {inception_ProjectionVersion} has expired.", version);
            return true;
        }

        ProjectionVersions allVersions = await GetAllVersionsAsync(version).ConfigureAwait(false);
        if (allVersions.IsOutdated(version))
        {
            logger.LogError("Projection `{inception_ProjectionVersion}` is outdated. There is a newer one which is already live.", version);
            return true;
        }
        else if (allVersions.IsCanceled(version) && version.ProjectionName.Equals(ProjectionVersionsHandler.ContractId, StringComparison.OrdinalIgnoreCase) == false)
        {
            logger.LogError("Projection {inception_ProjectionVersion} was canceled.", version);
            return true;
        }

        return false;
    }

    public IEnumerable<Type> GetInvolvedEventTypes(Type projectionType)
    {
        var ieventHandler = typeof(IEventHandler<>);
        var interfaces = projectionType.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == ieventHandler);
        foreach (var @interface in interfaces)
        {
            Type eventType = @interface.GetGenericArguments().First();
            yield return eventType;
        }
    }

    private ProjectionVersion GetNewProjectionVersion()
    {
        return new ProjectionVersion(ProjectionVersionsHandler.ContractId, ProjectionStatus.Live, 1, projectionHasher.CalculateHash(typeof(ProjectionVersionsHandler)));
    }

    private async Task<bool> IsVersionTrackerMissingAsync()
    {
        var versionId = new ProjectionVersionManagerId(ProjectionVersionsHandler.ContractId, contextAccessor.Context.Tenant);
        var result = await projectionReader.GetAsync<ProjectionVersionsHandler>(versionId).ConfigureAwait(false);

        return result.HasError || result.NotFound;
    }

    private bool HasReplayTimeout(DateTimeOffset replayUntil)
    {
        return DateTimeOffset.UtcNow >= replayUntil;
    }

    private bool IsNotSystemProjection(Type projectionType)
    {
        return typeof(ISystemProjection).IsAssignableFrom(projectionType) == false;
    }

    private async Task<IndexStatus> GetIndexStatusAsync<TIndex>() where TIndex : IEventStoreIndex
    {
        var id = new EventStoreIndexManagerId(typeof(TIndex).GetContractId(), contextAccessor.Context.Tenant);
        var result = await projectionReader.GetAsync<EventStoreIndexStatus>(id).ConfigureAwait(false);
        if (result.IsSuccess)
            return result.Data.State.Status;

        return IndexStatus.NotPresent;
    }

    private async Task<ProjectionVersions> GetAllVersionsAsync(ProjectionVersion version)
    {
        var versionId = new ProjectionVersionManagerId(version.ProjectionName, contextAccessor.Context.Tenant);
        var result = await projectionReader.GetAsync<ProjectionVersionsHandler>(versionId).ConfigureAwait(false);
        if (result.IsSuccess)
            return result.Data.State.AllVersions;

        return new ProjectionVersions();
    }
}
