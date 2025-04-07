namespace One.Inception.Projections.Versioning;

public interface IProjectionVersioningPolicy
{
    bool IsVersionable(string projectionName);
}
