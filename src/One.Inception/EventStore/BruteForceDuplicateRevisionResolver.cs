using System.Linq;

namespace One.Inception.EventStore;

public class BruteForceDuplicateRevisionResolver
{
    public EventStream Resolve(EventStream eventStream)
    {
        var errors = eventStream.Commits
            .GroupBy(x => x.Revision)
            .OrderBy(x => x.Key)
            .Select(x => x.First())
            .ToList();

        return new EventStream(errors);
    }
}
