using One.Inception.EventStore.Index.Handlers;
using One.Inception.Projections.Versioning;
using System;

namespace One.Inception.Projections;

public static class ProjectionExtensions
{
    public static bool IsProjectionVersionHandler(this string projectionName)
    {
        return projectionName.Equals(ProjectionVersionsHandler.ContractId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEventStoreIndexStatus(this string projectionName)
    {
        return projectionName.Equals(EventStoreIndexStatus.ContractId, StringComparison.OrdinalIgnoreCase);
    }
}
