﻿using One.Inception.EventStore.Players;
using System;
using System.Collections.Generic;

namespace One.Inception.Projections.Versioning;

public class ProjectionVersionManager : AggregateRoot<ProjectionVersionManagerState>
{
    ProjectionVersionManager() { }

    public ProjectionVersionManager(ProjectionVersionManagerId id, string hash)
    {
        string projectionName = id.Id;
        var initialVersion = new ProjectionVersion(projectionName, ProjectionStatus.New, 1, hash);
        var options = new ReplayEventsOptions();
        var timebox = new VersionRequestTimebox(DateTime.UtcNow);
        RequestVersion(id, initialVersion, options, timebox);
    }

    public void CancelVersionRequest(ProjectionVersion version, string reason)
    {
        if (CanCancel(version))
        {
            if (version.MaybeIsBroken())
            {
                foreach (ProjectionVersion buildingVersion in state.Versions.GetBuildingVersions())
                {
                    ProjectionVersionRequestCanceled reset = new ProjectionVersionRequestCanceled(state.Id, buildingVersion.WithStatus(ProjectionStatus.Canceled), reason + " Something wrong has happened. We are trying to reset the state so you could try rebuild/replay the state.");
                    Apply(reset);
                }
            }

            var @event = new ProjectionVersionRequestCanceled(state.Id, version.WithStatus(ProjectionStatus.Canceled), reason);
            Apply(@event);
        }
    }

    /// <summary>
    /// Replay all events into a new version of the projection.
    /// </summary>
    /// <param name="hash"></param>
    /// <param name="policy"></param>
    public void Replay(string hash, IProjectionVersioningPolicy policy, ReplayEventsOptions replayEventsOptions)
    {
        EnsureThereIsNoOutdatedBuildingVersions();

        if (CanReplay(hash, policy))
        {
            ProjectionVersion projectionVersion = state.Versions.GetNext(policy, hash);
            VersionRequestTimebox timebox = GetVersionRequestTimebox(hash);
            RequestVersion(state.Id, projectionVersion, replayEventsOptions, timebox);
        }
    }

    public void Rebuild(string hash, IProjectionVersioningPolicy policy, ReplayEventsOptions replayEventsOptions)
    {
        EnsureThereIsNoOutdatedBuildingVersions();

        // This is a special case so we can recover the projections in case of disaster recovery.
        if (state.Versions.ProjectionName == ProjectionVersionsHandler.ContractId)
        {
            ProjectionVersion currentLiveVersion = state.Versions.GetLive();
            if (currentLiveVersion is null)
            {
                var initialVersion = new ProjectionVersion(state.Id.Id, ProjectionStatus.New, 1, hash);
                RequestVersion(state.Id, initialVersion, replayEventsOptions, new VersionRequestTimebox(DateTime.UtcNow));
            }
            else
            {
                RequestVersion(state.Id, currentLiveVersion.WithStatus(ProjectionStatus.Fixing), replayEventsOptions, new VersionRequestTimebox(DateTime.UtcNow));
            }

            return;
        }

        if (CanRebuild(hash))
        {
            ProjectionVersion currentLiveVersion = state.Versions.GetLive();
            var timebox = GetVersionRequestTimebox(hash);
            if (currentLiveVersion is null)
            {
                var asd = state.Versions.GetNext(policy, hash);
                RequestVersion(state.Id, asd.WithStatus(ProjectionStatus.Fixing), replayEventsOptions, timebox);
            }
            else
            {
                RequestVersion(state.Id, currentLiveVersion.WithStatus(ProjectionStatus.Fixing), replayEventsOptions, timebox);
            }
        }
    }

    public void VersionRequestTimedout(ProjectionVersion version, VersionRequestTimebox timebox)
    {
        // TODO: check if the timebox really has expired LOL :), Believe me, do it
        // Ask the SAGA if this is for real??
        bool foundVersion = state.Versions.Contains(version);
        if (foundVersion == false) return;

        if (version.Status == ProjectionStatus.Fixing || version.Status == ProjectionStatus.New)
        {
            var @event = new ProjectionVersionRequestTimedout(state.Id, version.WithStatus(ProjectionStatus.Timedout), timebox);
            Apply(@event);
        }
    }

    public void PauseVersionRequest(ProjectionVersion version)
    {
        bool foundVersion = state.Versions.Contains(version);
        if (foundVersion == false) return;

        if (version.Status == ProjectionStatus.Fixing || version.Status == ProjectionStatus.New)
        {
            var @event = new ProjectionVersionRequestPaused(state.Id, version.WithStatus(ProjectionStatus.Timedout), DateTimeOffset.UtcNow);
            Apply(@event);
        }
    }

    public void NotifyHash(string hash, IProjectionVersioningPolicy policy, ReplayEventsOptions replayEventsOptions)
    {
        EnsureThereIsNoOutdatedBuildingVersions();

        if (ShouldReplay(hash))
        {
            Replay(hash, policy, replayEventsOptions);
        }
    }

    public void FinalizeVersionRequest(ProjectionVersion version)
    {
        var isVersionFound = state.Versions.Contains(version);
        if (isVersionFound)
        {
            var @event = new NewProjectionVersionIsNowLive(state.Id, version.WithStatus(ProjectionStatus.Live));
            Apply(@event);
        }

        EnsureThereIsNoOutdatedBuildingVersions();
    }

    private bool ShouldReplay(string hash)
    {
        bool isNewHashTheLiveOne = state.Versions.IsHashTheLiveOne(hash);
        bool isInProgress = state.Versions.IsInProgress();

        return isInProgress == false && (state.Versions.HasLiveVersion == false || isNewHashTheLiveOne == false);
    }

    private bool CanReplay(string hash, IProjectionVersioningPolicy policy)
    {
        bool isVersionable = state.Versions.IsVersionable(policy);
        bool replayInProgress = state.Versions.HasReplayInProgress();
        bool isNewHashTheLiveOne = state.Versions.IsHashTheLiveOne(hash);

        bool initialProjectionCreation = isVersionable == false && isNewHashTheLiveOne == false && state.Versions.IsInProgress() == false; // This function handles the creation of a new projection when the system is running for the first time, regardless of whether or not the projection is versionable and there is no live version available.

        return (replayInProgress == false && isVersionable) || initialProjectionCreation;
    }

    private bool CanRebuild(string hash)
    {
        ProjectionVersion currentLiveVersion = state.Versions.GetLive();
        if (currentLiveVersion is null)
            return true;

        bool hashMatchesCurrentLiveVersion = currentLiveVersion.Hash.Equals(hash);
        bool hasRebuildingVersion = state.Versions.HasRebuildingVersion();

        return hashMatchesCurrentLiveVersion && hasRebuildingVersion == false;
    }

    private void EnsureThereIsNoOutdatedBuildingVersions()
    {
        IEnumerable<ProjectionVersion> buildingVersions = state.Versions.GetBuildingVersions();

        foreach (var buildingVersion in buildingVersions)
        {
            if (state.LastVersionRequestTimebox.HasExpired)
                VersionRequestTimedout(buildingVersion, state.LastVersionRequestTimebox);

            if (state.Versions.HasLiveVersion && buildingVersion < state.Versions.GetLive())
                CancelVersionRequest(buildingVersion, "Outdated version. There is already a live version.");
        }
    }

    private bool CanCancel(ProjectionVersion version)
    {
        if (version.MaybeIsBroken())
            return true;

        if (version.Status != ProjectionStatus.New && version.Status != ProjectionStatus.Fixing)
            return false;

        return state.Versions.Contains(version);
    }

    /// <summary>
    /// When the same hash is requested multiple times for a Live hash we just make sure that the timeboxes are placed one after another
    /// In every other case we issue a timebox with immediate execution
    /// </summary>
    /// <param name="hash"></param>
    /// <returns>The timebox for the requested hash</returns>
    private VersionRequestTimebox GetVersionRequestTimebox(string hash)
    {
        ProjectionVersion live = state.Versions.GetLive();
        if (live == null) return new VersionRequestTimebox(DateTime.UtcNow);

        var hashesAreIdentical = string.Equals(live.Hash, hash, StringComparison.OrdinalIgnoreCase);
        if (hashesAreIdentical)
            return state.LastVersionRequestTimebox.GetNext();

        return new VersionRequestTimebox(DateTime.UtcNow);
    }

    private void RequestVersion(ProjectionVersionManagerId id, ProjectionVersion projectionVersion, ReplayEventsOptions replayEventsOptions, VersionRequestTimebox timebox)
    {
        var @event = new ProjectionVersionRequested(id, projectionVersion, replayEventsOptions, timebox);
        Apply(@event);
    }
}
