using System.Collections.Generic;
using One.Inception.IntegrityValidation;
using Microsoft.Extensions.Logging;

namespace One.Inception.EventStore.Integrity;

public sealed class EventStreamIntegrityPolicy : IIntegrityPolicy<EventStream>
{
    readonly List<IntegrityRule<EventStream>> rules;

    public EventStreamIntegrityPolicy(ILogger<EventStreamIntegrityPolicy> logger)
    {
        rules =
        [
            new IntegrityRule<EventStream>(new DuplicateRevisionsValidator()),
            new IntegrityRule<EventStream>(new OrderedRevisionsValidator(), new UnorderedRevisionsResolver()),
            new IntegrityRule<EventStream>(new MissingRevisionsValidator()),
        ];
    }

    public IEnumerable<IntegrityRule<EventStream>> Rules => rules;

    public IntegrityResult<EventStream> Apply(EventStream eventStream)
    {
        var integrity = new IntegrityResult<EventStream>(eventStream, false);

        foreach (IntegrityRule<EventStream> rule in rules)
        {
            IValidatorResult validatorResult = rule.Validator.Validate(eventStream);
            if (validatorResult.IsValid == false)
                integrity = rule.Resolver.Resolve(eventStream, validatorResult);
        }

        return integrity;
    }
}
