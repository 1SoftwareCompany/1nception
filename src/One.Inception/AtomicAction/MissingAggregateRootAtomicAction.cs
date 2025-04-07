using System;
using System.Threading.Tasks;
using One.Inception.Userfull;

namespace One.Inception.AtomicAction;

public sealed class MissingAggregateRootAtomicAction : IAggregateRootAtomicAction, ILock
{
    private const string MissingMessage = "The AggregateRootAtomicAction is not configured. Please install a nuget package which provides aggregate sync capabilities such as IAggregateRootAtomicAction. ex.: 1nception.AtomicAction.Redis. You can disable the AtomicAction functionality with Inception:ApplicationServicesEnabled = false";

    public Task<Result<bool>> ExecuteAsync(AggregateRootId aggregateRootId, int aggregateRootRevision, Func<Task> action) => throw new NotImplementedException(MissingMessage);
    public void Dispose() => throw new NotImplementedException(MissingMessage);

    public Task<bool> IsLockedAsync(string resource) => throw new NotImplementedException(MissingMessage);
    public Task<bool> LockAsync(string resource, TimeSpan ttl) => throw new NotImplementedException(MissingMessage);
    public Task UnlockAsync(string resource) => throw new NotImplementedException(MissingMessage);
}

