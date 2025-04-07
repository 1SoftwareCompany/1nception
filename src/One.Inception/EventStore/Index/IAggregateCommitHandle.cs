using System.Threading.Tasks;

namespace One.Inception.EventStore.Index;

public interface IAggregateCommitHandle<in T>
    where T : AggregateCommit
{
    Task HandleAsync(T @event);
}
