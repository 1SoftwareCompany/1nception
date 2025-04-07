using System;

namespace One.Inception.Projections.Versioning;

public class MarkupInterfaceProjectionVersioningPolicy : IProjectionVersioningPolicy
{
    public bool IsVersionable(string projectionName)
    {
        try
        {
            return typeof(INonVersionableProjection).IsAssignableFrom(MessageInfo.GetTypeByContract(projectionName)) == false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
