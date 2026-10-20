using System;
using System.Threading.Tasks;
using One.Inception.Userfull;

namespace One.Inception.AtomicAction;

public interface IAggregateRootAtomicAction : IDisposable
{
    Task<Result<bool>> ExecuteAsync(AtomicActionResource resource, int aggregateRootRevision, Func<Task> action);
}
