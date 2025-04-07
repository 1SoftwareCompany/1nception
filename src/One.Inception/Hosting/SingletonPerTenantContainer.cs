using One.Inception.MessageProcessing;
using One.Inception.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;

namespace One.Inception;

public class SingletonPerTenantContainer<T> : IDisposable
{
    public SingletonPerTenantContainer()
    {
        Stash = new ConcurrentDictionary<string, T>();
    }

    public ConcurrentDictionary<string, T> Stash { get; private set; }

    public void Dispose()
    {
        foreach (var item in Stash.Values)
        {
            if (item is IDisposable disposableItem)
                disposableItem.Dispose();
        }
        Stash.Clear();
    }
}

// TODO: mynkow
public class SingletonPerTenant<T>
{
    private readonly SingletonPerTenantContainer<T> container;
    private readonly IInceptionContextAccessor contextAccessor;

    public SingletonPerTenant(SingletonPerTenantContainer<T> container, IInceptionContextAccessor contextAccessor)
    {
        if (contextAccessor is null) throw new ArgumentNullException(nameof(contextAccessor));
        this.container = container;
        this.contextAccessor = contextAccessor;
    }

    public T Get()
    {
        if (container.Stash.TryGetValue(contextAccessor.Context.Tenant, out T instance) == false)
        {
            instance = contextAccessor.Context.ServiceProvider.GetRequiredService<T>();
            instance.AssignPropertySafely<IHaveTenant>(x => x.Tenant = contextAccessor.Context.Tenant);
            container.Stash.TryAdd(contextAccessor.Context.Tenant, instance);
        }

        return instance;
    }
}
