using System.Collections.Generic;
using System.Linq;
using One.Inception.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Discoveries;

public class MigrationsDiscovery : HandlersDiscovery<IMigrationHandler>
{
    protected override DiscoveryResult<IMigrationHandler> DiscoverFromAssemblies(DiscoveryContext context)
    {
        IEnumerable<DiscoveredModel> models =
            DiscoverHandlers(context)
            .Concat(DiscoverCustomLogic(context));

        return new DiscoveryResult<IMigrationHandler>(models);
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverCustomLogic(DiscoveryContext context)
    {
        // These should be transient till the ProjectionRepository is transient.
        yield return new DiscoveredModel(typeof(IMigrationCustomLogic), typeof(NoCustomLogic), ServiceLifetime.Transient)
        {
            CanOverrideDefaults = true
        };
        yield return new DiscoveredModel(typeof(IInceptionMigrator), typeof(InceptionMigrator), ServiceLifetime.Transient);

        // These are for manual start of the migration process, usually in a separate process. These are safe to be (tenant)Singleton
        yield return new DiscoveredModel(typeof(InceptionMigrator), typeof(InceptionMigrator), ServiceLifetime.Transient);
        yield return new DiscoveredModel(typeof(IInceptionMigratorManual), provider => provider.GetRequiredService<SingletonPerTenant<InceptionMigrator>>().Get(), ServiceLifetime.Transient);
    }
}
