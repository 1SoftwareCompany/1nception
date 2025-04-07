using System;
using System.Threading.Tasks;
using One.Inception.Projections.Cassandra;

namespace One.Inception.Projections;

internal sealed class MissingProjections : IProjectionStore, IInitializableProjectionStore
{
    private const string MissingProjectionsMessage = "The Projections feature is not installed or properly configured. Please install a nuget package which provides IProjectionStore capabilities. ex.: 1nception.Projections.Cassandra. You can disable the projections functionality with Inception:ProjectionsEnabled = false";

    public Task EnumerateProjectionsAsync(ProjectionsOperator @operator, ProjectionQueryOptions options)
    {
        throw new NotImplementedException(MissingProjectionsMessage);
    }

    public Task<bool> InitializeAsync(ProjectionVersion version)
    {
        throw new NotImplementedException(MissingProjectionsMessage);
    }

    public Task SaveAsync(ProjectionCommit commit)
    {
        throw new NotImplementedException(MissingProjectionsMessage);
    }
}
