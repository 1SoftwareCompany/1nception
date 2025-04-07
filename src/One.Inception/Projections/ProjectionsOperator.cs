using One.Inception.EventStore;
using System;
using System.Threading.Tasks;

namespace One.Inception.Projections;

public class ProjectionsOperator
{
    public Func<IEvent, Task> OnProjectionEventLoadedAsync { get; set; }

    public Func<ProjectionStream, Task> OnProjectionStreamLoadedAsync { get; set; }

    public Func<ProjectionStream, PagingOptions, Task> OnProjectionStreamLoadedWithPagingAsync { get; set; }
}
