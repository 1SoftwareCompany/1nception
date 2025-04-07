using System.Threading.Tasks;

namespace One.Inception.MessageProcessing;

public sealed class AggregateRepositoryGate : IAggregateRepository
{
    readonly IAggregateRepository aggregateRepository;

    public AggregateRepositoryGate(IAggregateRepository repository)
    {
        this.aggregateRepository = repository;
    }

    public Task<ReadResult<AR>> LoadAsync<AR>(AggregateRootId id) where AR : IAggregateRoot
    {
        return aggregateRepository.LoadAsync<AR>(id);
    }

    public Task SaveAsync<AR>(AR aggregateRoot) where AR : IAggregateRoot
    {
        return aggregateRepository.SaveAsync(aggregateRoot);
    }
}
