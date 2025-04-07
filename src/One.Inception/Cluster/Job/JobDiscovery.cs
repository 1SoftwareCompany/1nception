using System.Collections.Generic;
using System.Linq;
using One.Inception.Discoveries;
using One.Inception.EventStore;
using One.Inception.EventStore.Index;
using One.Inception.EventStore.Players;
using One.Inception.Projections;
using One.Inception.Projections.Rebuilding;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Cluster.Job.InMemory;

public class JobDiscovery : DiscoveryBase<IInceptionJob<object>>
{
    protected override DiscoveryResult<IInceptionJob<object>> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<IInceptionJob<object>>(GetModels(context).Concat(GetEventStoreJobs(context)));
    }

    private IEnumerable<DiscoveredModel> GetModels(DiscoveryContext context)
    {
        IEnumerable<System.Type> jobs = context.FindService<IInceptionJob<object>>();

        foreach (var job in jobs)
        {
            yield return new DiscoveredModel(job, job, ServiceLifetime.Transient);
        }

        yield return new DiscoveredModel(typeof(TypeContainer<IInceptionJob<object>>), new TypeContainer<IInceptionJob<object>>(jobs));

        yield return new DiscoveredModel(typeof(InMemoryInceptionJobRunner), typeof(InMemoryInceptionJobRunner), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IInceptionJobRunner), typeof(InMemoryInceptionJobRunner), ServiceLifetime.Transient);

        yield return new DiscoveredModel(typeof(IJobNameBuilder), typeof(DefaultJobNameBuilder), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(DefaultJobNameBuilder), typeof(DefaultJobNameBuilder), ServiceLifetime.Singleton);
    }

    private IEnumerable<DiscoveredModel> GetEventStoreJobs(DiscoveryContext context)
    {
        var hasProjectionStore = context.FindService<IProjectionStore>().Any();
        bool hasEventStore = context.FindServiceExcept<IEventStore>(typeof(InceptionEventStore)).Any();

        //if (hasEventStore)
        {
            yield return new DiscoveredModel(typeof(IRebuildIndex_EventToAggregateRootId_JobFactory), typeof(RebuildIndex_EventToAggregateRootId_JobFactory), ServiceLifetime.Transient);
            yield return new DiscoveredModel(typeof(RebuildIndex_MessageCounter_JobFactory), typeof(RebuildIndex_MessageCounter_JobFactory), ServiceLifetime.Transient);
            yield return new DiscoveredModel(typeof(ReplayPublicEvents_JobFactory), typeof(ReplayPublicEvents_JobFactory), ServiceLifetime.Transient);

            //  if (hasProjectionStore)
            {
                yield return new DiscoveredModel(typeof(RebuildProjection_JobFactory), typeof(RebuildProjection_JobFactory), ServiceLifetime.Transient);
                yield return new DiscoveredModel(typeof(RebuildProjectionSequentially_JobFactory), typeof(RebuildProjectionSequentially_JobFactory), ServiceLifetime.Transient);
            }
        }
    }
}
