using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using One.Inception.EventStore;
using One.Inception.EventStore.Integrity;
using One.Inception.IntegrityValidation;
using Machine.Specifications;

namespace One.Inception.Tests.ValidatorsAndResolvers;

[Subject("IntegrityValidation")]
public class When__EventStream__has_many_revisions
{
    Establish context = () =>
    {
        byte[] aggregateId = Guid.NewGuid().ToByteArray();
        long timestamp = DateTimeOffset.Now.ToFileTime();
        List<AggregateCommit> commits = Enumerable.Range(1, 100_000)
            .Where(revision => revision != 50_000)
            .Select(revision => new AggregateCommit(aggregateId, revision, new List<IEvent>(), new List<IPublicEvent>(), timestamp))
            .ToList();
        eventStream = new EventStream(commits);
        validator = new MissingRevisionsValidator();
    };

    Because of = () =>
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        validationResult = validator.Validate(eventStream);
        elapsed = stopwatch.Elapsed;
    };

    It should_report_only_the_missing_revision = () => validationResult.Errors.Single().ShouldContain("'50000'");

    // A quadratic check needs billions of comparisons here, and it cannot be cancelled while the app is stopping.
    It should_stay_fast =() => elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));

    static EventStream eventStream;
    static MissingRevisionsValidator validator;
    static IValidatorResult validationResult;
    static TimeSpan elapsed;
}
