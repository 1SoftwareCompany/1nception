using System.Collections.Generic;

namespace One.Inception.Projections;

public interface IProjectionVersionFinder
{
    IEnumerable<ProjectionVersion> GetProjectionVersionsToBootstrap();
}
