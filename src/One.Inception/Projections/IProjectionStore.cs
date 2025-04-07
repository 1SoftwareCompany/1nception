using One.Inception.Projections.Cassandra;
using System.Threading.Tasks;

namespace One.Inception.Projections;

public interface IProjectionStore
{

    Task EnumerateProjectionsAsync(ProjectionsOperator @operator, ProjectionQueryOptions options);

    Task SaveAsync(ProjectionCommit commit);
}

public interface IInitializableProjectionStore
{
    Task<bool> InitializeAsync(ProjectionVersion version);
}
