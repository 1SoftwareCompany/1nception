using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace One.Inception.EventStore;

public sealed class AggregateStream
{
    public AggregateStream(IEnumerable<AggregateEventRaw> events)
    {
        Commits = new List<AggregateCommitRaw>();
        IEnumerable<IGrouping<int, AggregateEventRaw>> byRevision = events.GroupBy(x => x.Revision);
        foreach (IGrouping<int, AggregateEventRaw> revisionEvents in byRevision)
        {
            long timestamp = revisionEvents.First().Timestamp; // all events in revision must be with the same timestamp
            AggregateCommitRaw commit = new AggregateCommitRaw(revisionEvents, timestamp.ToDateTimeOffsetUtc());
            Commits.Add(commit);
        }
    }

    public List<AggregateCommitRaw> Commits { get; init; }
}

public sealed class EventStream
{
    private readonly IList<AggregateCommit> aggregateCommits;

    public EventStream(IList<AggregateCommit> aggregateCommits)
    {
        this.aggregateCommits = aggregateCommits;
    }

    public IEnumerable<AggregateCommit> Commits { get { return aggregateCommits; } }

    public bool TryRestoreFromHistory<T>(out T aggregateRoot) where T : IAmEventSourced
        => TryRestoreFromHistory(out aggregateRoot, CancellationToken.None);

    public bool TryRestoreFromHistory<T>(out T aggregateRoot, CancellationToken cancellationToken) where T : IAmEventSourced
    {
        //http://www.datastax.com/dev/blog/client-side-improvements-in-cassandra-2-0
        aggregateRoot = default(T);
        var events = aggregateCommits.SelectMany(x => x.Events);
        if (events.Any())
        {
            int currentRevision = aggregateCommits.Last().Revision;
            aggregateRoot = (T)FastActivator.CreateInstance(typeof(T), true);
            aggregateRoot.ReplayEvents(CancelBetweenEvents(events, cancellationToken), currentRevision);
            return true;
        }
        else
        {
            return false;
        }
    }

    private static IEnumerable<IEvent> CancelBetweenEvents(IEnumerable<IEvent> events, CancellationToken cancellationToken)
    {
        foreach (IEvent @event in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return @event;
        }
    }

    public override string ToString()
    {
        var performanceCriticalOutputBuilder = new StringBuilder();
        AggregateCommit firstCommit = aggregateCommits.First();
        string aggregateRootId = Encoding.UTF8.GetString(firstCommit.AggregateRootId.Span);
        string aggregateName = Encoding.UTF8.GetString(firstCommit.AggregateRootId.Span).Split('@')[0];

        performanceCriticalOutputBuilder.AppendLine("Aggregate Info");
        performanceCriticalOutputBuilder.AppendLine("==============");
        performanceCriticalOutputBuilder.AppendLine($"- Aggregate root ID : `{aggregateRootId}`");
        performanceCriticalOutputBuilder.AppendLine($"- Aggregate name: `{aggregateName}`");
        performanceCriticalOutputBuilder.AppendLine();
        performanceCriticalOutputBuilder.AppendLine("## Commits");

        foreach (var commit in aggregateCommits)
        {
            performanceCriticalOutputBuilder.AppendLine();
            performanceCriticalOutputBuilder.AppendLine($"#### Revision {commit.Revision}");

            foreach (var @event in commit.Events)
            {
                performanceCriticalOutputBuilder.AppendLine($"- {@event}");
            }
        }
        performanceCriticalOutputBuilder.AppendLine("-------------------------------------------------------------------------------------------------------------------");

        return performanceCriticalOutputBuilder.ToString();
    }
}
