using System;
using System.Collections.Generic;
using One.Inception.EventStore;
using One.Inception.EventStore.Integrity;
using One.Inception.IntegrityValidation;
using Machine.Specifications;

namespace One.Inception.Tests.ValidatorsAndResolvers;

[Subject("IntegrityValidation")]
public class When__AggregateCommit__has_unordered_revisions
{
    Establish context = () =>
        {
            byte[] aggregateId = Guid.NewGuid().ToByteArray();
            AggregateCommit commit1 = new AggregateCommit(aggregateId, 1, new List<IEvent>(), new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
            AggregateCommit commit2 = new AggregateCommit(aggregateId, 2, new List<IEvent>(), new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
            AggregateCommit commit4 = new AggregateCommit(aggregateId, 4, new List<IEvent>(), new List<IPublicEvent>(), DateTimeOffset.Now.ToFileTime());
            eventStream = new EventStream(new[] { commit2, commit4, commit1 });
            validator = new OrderedRevisionsValidator();

        };

    Because of = () => validationResult = validator.Validate(eventStream);

    It should_report_about_the_invalid__EventStream__ = () => validationResult.IsValid.ShouldBeFalse();

    It should_have__OrderedRevisionsValidator_as_error_type = () => validationResult.ErrorType.ShouldEqual(nameof(OrderedRevisionsValidator));

    static EventStream eventStream;
    static OrderedRevisionsValidator validator;
    static IValidatorResult validationResult;
}
