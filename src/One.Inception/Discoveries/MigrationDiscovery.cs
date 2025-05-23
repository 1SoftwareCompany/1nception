﻿using System.Collections.Generic;
using One.Inception.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Discoveries;

public sealed class MigrationDiscovery : DiscoveryBase<MigrationDiscovery>
{
    protected override DiscoveryResult<MigrationDiscovery> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<MigrationDiscovery>(GetModels(context));
    }

    IEnumerable<DiscoveredModel> GetModels(DiscoveryContext context)
    {
        yield return new DiscoveredModel(typeof(CopyEventStore<,>), typeof(CopyEventStore<,>), ServiceLifetime.Transient);
    }
}
