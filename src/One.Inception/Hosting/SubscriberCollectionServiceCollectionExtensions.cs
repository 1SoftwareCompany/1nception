using One.Inception.Discoveries;
using One.Inception.EventStore.Index;
using One.Inception.MessageProcessing;
using One.Inception.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception;

public static class SubscriberCollectionServiceCollectionExtensions
{
    public static IServiceCollection AddSubscribers<T>(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISubscriberCollection<T>), typeof(SubscriberCollection<T>));
        services.AddSingleton(typeof(ISubscriberFinder<T>), typeof(SubscriberFinder<T>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<T>), typeof(DefaultSubscriberWorkflow<T>));
        services.AddSingleton(typeof(ISubscriberFactory<T>), typeof(HandlerSubscriberFactory<T>));

        return services;
    }

    public static IServiceCollection AddDefaultSubscribers(this IServiceCollection services, InceptionServicesProvider provider)
    {
        var options = new InceptionHostOptions();
        provider.Configuration.GetSection("Inception").Bind(options);

        services.AddSubscribersWithOpenGenerics();

        if (options.ApplicationServicesEnabled)
            services.AddApplicationServiceSubscribers();

        services.AddSubscribers<IPort>();
        services.AddSubscribers<IGateway>();
        services.AddSubscribers<ISaga>();
        services.AddSubscribers<ISystemAppService>();
        services.AddSubscribers<ISystemPort>();
        services.AddSubscribers<ISystemSaga>();
        services.AddSubscribers<ISystemProjection>();
        services.AddSubscribers<IMigrationHandler>();
        services.AddTriggersSubscribers();

        services.AddEventStoreIndexSubscribers();
        services.AddSystemEventStoreIndexSubscribers();

        if (options.ProjectionsEnabled)
            services.AddProjections();

        return services;
    }

    public static IServiceCollection AddSubscribersWithOpenGenerics(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISubscriberCollection<>), typeof(SubscriberCollection<>));
        services.AddSingleton(typeof(ISubscriberFinder<>), typeof(SubscriberFinder<>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<>), typeof(DefaultSubscriberWorkflow<>));
        services.AddSingleton(typeof(ISubscriberFactory<>), typeof(HandlerSubscriberFactory<>));

        return services;
    }

    public static IServiceCollection AddApplicationServiceSubscribers(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISubscriberCollection<IApplicationService>), typeof(SubscriberCollection<IApplicationService>));
        services.AddSingleton(typeof(ISubscriberFinder<IApplicationService>), typeof(SubscriberFinder<IApplicationService>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<IApplicationService>), typeof(ApplicationServiceSubscriberWorkflow));
        services.AddSingleton(typeof(ISubscriberFactory<IApplicationService>), typeof(HandlerSubscriberFactory<IApplicationService>));

        return services;
    }

    public static IServiceCollection AddTriggersSubscribers(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISubscriberCollection<ITrigger>), typeof(SubscriberCollection<ITrigger>));
        services.AddSingleton(typeof(ISubscriberFinder<ITrigger>), typeof(SubscriberFinder<ITrigger>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<ITrigger>), typeof(TriggersSubscriberWorkflow<ITrigger>));
        services.AddSingleton(typeof(ISubscriberFactory<ITrigger>), typeof(HandlerSubscriberFactory<ITrigger>));

        services.AddSingleton(typeof(ISubscriberCollection<ISystemTrigger>), typeof(SubscriberCollection<ISystemTrigger>));
        services.AddSingleton(typeof(ISubscriberFinder<ISystemTrigger>), typeof(SubscriberFinder<ISystemTrigger>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<ISystemTrigger>), typeof(TriggersSubscriberWorkflow<ISystemTrigger>));
        services.AddSingleton(typeof(ISubscriberFactory<ISystemTrigger>), typeof(HandlerSubscriberFactory<ISystemTrigger>));
        return services;
    }

    public static IServiceCollection AddEventStoreIndexSubscribers(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISubscriberCollection<IEventStoreIndex>), typeof(SubscriberCollection<IEventStoreIndex>));
        services.AddSingleton(typeof(ISubscriberFinder<IEventStoreIndex>), typeof(SubscriberFinder<IEventStoreIndex>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<IEventStoreIndex>), typeof(EventStoreIndexSubscriberWorkflow<IEventStoreIndex>));
        services.AddSingleton(typeof(ISubscriberFactory<IEventStoreIndex>), typeof(EventStoreIndexSubscriberFactory<IEventStoreIndex>));

        return services;
    }

    public static IServiceCollection AddSystemEventStoreIndexSubscribers(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ISubscriberCollection<ISystemEventStoreIndex>), typeof(SubscriberCollection<ISystemEventStoreIndex>));
        services.AddSingleton(typeof(ISubscriberFinder<ISystemEventStoreIndex>), typeof(SubscriberFinder<ISystemEventStoreIndex>));
        services.AddSingleton(typeof(ISubscriberWorkflowFactory<ISystemEventStoreIndex>), typeof(EventStoreIndexSubscriberWorkflow<ISystemEventStoreIndex>));
        services.AddSingleton(typeof(ISubscriberFactory<ISystemEventStoreIndex>), typeof(EventStoreIndexSubscriberFactory<ISystemEventStoreIndex>));

        return services;
    }

    public static IServiceCollection AddProjections(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ProjectionSubscriberFinder));
        services.AddSingleton(typeof(ISubscriberFinder<IProjection>), typeof(ProjectionSubscriberFinder));

        return services;
    }
}
