using One.Inception.EventStore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace One.Inception.MessageProcessing;

internal sealed class AggregateRepositoryAndEventPublisher : IAggregateRepository
{
    readonly AggregateRepository aggregateRepository;
    readonly IPublisher<IEvent> eventPublisher;
    private readonly IPublisher<IPublicEvent> publicEventPublisher;
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly ILogger<AggregateRepositoryAndEventPublisher> logger;

    public AggregateRepositoryAndEventPublisher(AggregateRepository repository, IPublisher<IEvent> eventPublisher, IPublisher<IPublicEvent> publicEventPublisher, IInceptionContextAccessor contextAccessor, ILogger<AggregateRepositoryAndEventPublisher> logger)
    {
        this.aggregateRepository = repository;
        this.eventPublisher = eventPublisher;
        this.publicEventPublisher = publicEventPublisher;
        this.contextAccessor = contextAccessor;
        this.logger = logger;
    }

    public Task<ReadResult<AR>> LoadAsync<AR>(AggregateRootId id) where AR : IAggregateRoot
    {
        return aggregateRepository.LoadAsync<AR>(id);
    }

    public async Task SaveAsync<AR>(AR aggregateRoot) where AR : IAggregateRoot
    {
        AggregateCommit aggregateCommit = await aggregateRepository.SaveInternalAsync(aggregateRoot).ConfigureAwait(false);

        if (aggregateRoot.UncommittedEvents is null || aggregateRoot.UncommittedEvents.Any() == false)
            return;

        bool isEverythingPublished = true;
        int position = -1;
        foreach (IEvent theEvent in aggregateRoot.UncommittedEvents)
        {
            if (theEvent is EntityEvent entityEvent)
            {
                isEverythingPublished &= await eventPublisher.PublishAsync(entityEvent.Event, BuildHeaders(aggregateCommit, aggregateRoot, ++position)).ConfigureAwait(false);
            }
            else
            {
                isEverythingPublished &= await eventPublisher.PublishAsync(theEvent, BuildHeaders(aggregateCommit, aggregateRoot, ++position)).ConfigureAwait(false);
            }
        }
        position += 5;
        foreach (IPublicEvent publicEvent in aggregateRoot.UncommittedPublicEvents)
        {
            isEverythingPublished &= await publicEventPublisher.PublishAsync(publicEvent, BuildHeaders(aggregateCommit, aggregateRoot, position++)).ConfigureAwait(false);
        }

        if (isEverythingPublished == false)
            logger.LogError("Aggregate events have been published partially.");
    }

    Dictionary<string, string> BuildHeaders(AggregateCommit aggregatecommit, IAggregateRoot aggregateRoot, int eventPosition)
    {
        Dictionary<string, string> messageHeaders = new Dictionary<string, string>();

        messageHeaders.Add(MessageHeader.AggregateRootId, aggregateRoot.State.Id.Value);
        messageHeaders.Add(MessageHeader.AggregateRootRevision, aggregateRoot.Revision.ToString());
        messageHeaders.Add(MessageHeader.AggregateRootEventPosition, eventPosition.ToString());
        messageHeaders.Add(MessageHeader.AggregateCommitTimestamp, aggregatecommit.Timestamp.ToString());

        return messageHeaders;
    }
}
