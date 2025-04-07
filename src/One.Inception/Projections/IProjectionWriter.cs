using System;
using System.Threading.Tasks;

namespace One.Inception.Projections;

public interface IProjectionWriter
{
    Task SaveAsync(Type projectionType, IEvent @event);
    Task SaveAsync(Type projectionType, IEvent @event, ProjectionVersion version);
}
