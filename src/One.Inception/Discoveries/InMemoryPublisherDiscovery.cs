﻿using System.Collections.Generic;
using One.Inception.AtomicAction;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Discoveries;

public class InMemoryDiscovery : DiscoveryBase<IPublisher<IMessage>>
{
    protected override DiscoveryResult<IPublisher<IMessage>> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<IPublisher<IMessage>>(GetModels());
    }

    IEnumerable<DiscoveredModel> GetModels()
    {
        yield return new DiscoveredModel(typeof(IAggregateRootAtomicAction), typeof(MissingAggregateRootAtomicAction), ServiceLifetime.Singleton);
        yield return new DiscoveredModel(typeof(ILock), typeof(MissingAggregateRootAtomicAction), ServiceLifetime.Singleton);

        // The order matters here. If you reorder the registrations bellow then you know what you are doing.
        yield return new DiscoveredModel(typeof(DelegatingPublishHandler), typeof(LoggingPublishHandler), ServiceLifetime.Transient) { CanAddMultiple = true };
        yield return new DiscoveredModel(typeof(DelegatingPublishHandler), typeof(HeadersPublishHandler), ServiceLifetime.Transient) { CanAddMultiple = true };
        yield return new DiscoveredModel(typeof(DelegatingPublishHandler), typeof(ActivityPublishHandler), ServiceLifetime.Transient) { CanAddMultiple = true };
    }
}
