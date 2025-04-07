using System;
using System.Collections.Generic;
using System.Linq;
using One.Inception.AutoUpdates;
using One.Inception.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Discoveries;

public class InceptionHostDiscovery : DiscoveryBase<IInceptionHost>
{
    protected override DiscoveryResult<IInceptionHost> DiscoverFromAssemblies(DiscoveryContext context)
    {
        IEnumerable<DiscoveredModel> models =
            DiscoverInceptionHost(context)
            .Concat(DiscoverStartups(context))
            .Concat(DiscoverCommands(context))
            .Concat(DiscoverEvents(context))
            .Concat(DiscoverPublicEvents(context))
            .Concat(DiscoverSignals(context))
            .Concat(DiscoverMigrations(context))
            .Concat(DiscoverAutoUpdates(context))
            .Concat(DiscoverTenantStartups(context));

        return new DiscoveryResult<IInceptionHost>(models);
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverInceptionHost(DiscoveryContext context)
    {
        return DiscoverModel<IInceptionHost, InceptionHost>(ServiceLifetime.Transient);
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverStartups(DiscoveryContext context)
    {
        foreach (var startupType in context.Assemblies.Find<IInceptionStartup>())
        {
            yield return new DiscoveredModel(startupType, startupType, ServiceLifetime.Singleton);
        }

        yield return new DiscoveredModel(typeof(ProjectionsStartup), typeof(ProjectionsStartup), ServiceLifetime.Transient); // TODO: Check if this is alrady registered in the foreach above. If yes we can remove this line. Elase we have to figure out what is going on
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverTenantStartups(DiscoveryContext context)
    {
        foreach (var startupType in context.Assemblies.Find<ITenantStartup>())
        {
            var singletonPerTenantOpenGeneric = typeof(SingletonPerTenant<>);
            Type[] typeArgs = { startupType };
            var ggwp = singletonPerTenantOpenGeneric.MakeGenericType(typeArgs);

            yield return new DiscoveredModel(typeof(ITenantStartup), provider => ((SingletonPerTenant<ITenantStartup>)provider.GetRequiredService(ggwp)).Get(), ServiceLifetime.Transient) { CanAddMultiple = true };
            yield return new DiscoveredModel(startupType, startupType, ServiceLifetime.Transient);
        }
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverCommands(DiscoveryContext context)
    {
        IEnumerable<Type> loadedCommands = context.Assemblies.Find<ICommand>();
        yield return new DiscoveredModel(typeof(TypeContainer<ICommand>), new TypeContainer<ICommand>(loadedCommands));
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverEvents(DiscoveryContext context)
    {
        IEnumerable<Type> loadedEvents = context.Assemblies.Find<IEvent>();
        yield return new DiscoveredModel(typeof(TypeContainer<IEvent>), new TypeContainer<IEvent>(loadedEvents));
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverPublicEvents(DiscoveryContext context)
    {
        IEnumerable<Type> loadedPublicEvents = context.Assemblies.Find<IPublicEvent>();
        yield return new DiscoveredModel(typeof(TypeContainer<IPublicEvent>), new TypeContainer<IPublicEvent>(loadedPublicEvents));
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverSignals(DiscoveryContext context)
    {
        IEnumerable<Type> loadedSignals = context.Assemblies.Find<ISignal>();
        yield return new DiscoveredModel(typeof(TypeContainer<ISignal>), new TypeContainer<ISignal>(loadedSignals));
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverMigrations(DiscoveryContext context)
    {
        var options = new InceptionHostOptions();
        context.Configuration.GetSection("Inception").Bind(options);

        if (options.MigrationsEnabled)
        {
            IEnumerable<Type> loadedMigrations = context.Assemblies.Find<IMigration>();

            foreach (var migrationType in loadedMigrations)
            {
                var directInterfaces = GetDirectInterfaces(migrationType);

                foreach (var interfaceBase in directInterfaces)
                {
                    var generics = interfaceBase.GetGenericArguments();
                    if (generics.Length == 1 && typeof(IMigration).IsAssignableFrom(interfaceBase))
                    {
                        var tenantResolverType = typeof(IMigration<>);
                        var tenantResolverTypeGeneric = tenantResolverType.MakeGenericType(generics.Single());
                        yield return new DiscoveredModel(tenantResolverTypeGeneric, migrationType, ServiceLifetime.Transient)
                        {
                            CanAddMultiple = true
                        };
                    }
                }
            }
        }

        static IEnumerable<Type> GetDirectInterfaces(Type type)
        {
            var allInterfaces = new List<Type>();
            var childInterfaces = new List<Type>();
            foreach (var i in type.GetInterfaces())
            {
                allInterfaces.Add(i);
                foreach (var ii in i.GetInterfaces())
                    childInterfaces.Add(ii);
            }
            var directInterfaces = allInterfaces.Except(childInterfaces);

            return directInterfaces;
        }
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverAutoUpdates(DiscoveryContext context)
    {
        IEnumerable<Type> loadedMigrations = context.Assemblies.Find<IAutoUpdate>();
        foreach (Type type in loadedMigrations)
        {
            yield return new DiscoveredModel(typeof(IAutoUpdate), type, ServiceLifetime.Transient) { CanAddMultiple = true };

            foreach (Type migrationType in loadedMigrations)
            {
                var singletonPerTenantOpenGeneric = typeof(SingletonPerTenant<>);
                Type[] typeArgs = { migrationType };
                var ggwp = singletonPerTenantOpenGeneric.MakeGenericType(typeArgs);
                yield return new DiscoveredModel(ggwp, provider => ((SingletonPerTenant<IAutoUpdate>)provider.GetRequiredService(ggwp)).Get(), ServiceLifetime.Transient) { CanOverrideDefaults = true };
            }
        }

        yield return new DiscoveredModel(typeof(IAutoUpdaterStrategy), provider => provider.GetRequiredService<SingletonPerTenant<AutoUpdaterStrategy>>().Get(), ServiceLifetime.Transient) { CanOverrideDefaults = true };
        yield return new DiscoveredModel(typeof(AutoUpdaterStrategy), typeof(AutoUpdaterStrategy), ServiceLifetime.Transient);
    }
}
