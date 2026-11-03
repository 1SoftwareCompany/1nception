using System.Collections.Generic;
using System.Linq;
using One.Inception.IntegrityValidation;

namespace One.Inception.EventStore.Integrity;

public sealed class MissingRevisionsValidator : IValidator<EventStream>
{
    public uint PriorityLevel => 300;

    public int CompareTo(IValidator<EventStream> other)
    {
        return PriorityLevel.CompareTo(other.PriorityLevel);
    }

    public IValidatorResult Validate(EventStream candidate)
    {
        return new ValidatorResult(GetErrorMessages(candidate), nameof(OrderedRevisionsValidator));
    }

    private IEnumerable<string> GetErrorMessages(EventStream eventStream)
    {
        HashSet<int> revisions = eventStream.Commits.Select(x => x.Revision).ToHashSet();
        int maxRevision = revisions.Count > 0 ? revisions.Max() : 0;

        for (int expectedRevision = 1; expectedRevision <= maxRevision; expectedRevision++)
        {
            if (revisions.Contains(expectedRevision) == false)
            {
                yield return $"Missing {nameof(AggregateCommit)} revision '{expectedRevision}'.";
            }
        }
    }
}
