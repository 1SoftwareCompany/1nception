using System;
using System.Collections.Concurrent;
using System.Linq;

namespace One.Inception.Projections;

public static class ProjectionInfoCache
{
    private static readonly ConcurrentDictionary<Type, bool> typeToPersistence = new ConcurrentDictionary<Type, bool>();
    private static readonly ConcurrentDictionary<Type, bool> typeToOrder = new ConcurrentDictionary<Type, bool>();
    private static readonly ConcurrentDictionary<Type, DateTimeOffset?> typeToAfterTimestamp = new ConcurrentDictionary<Type, DateTimeOffset?>();

    public static bool IsPersistedProjection(this Type messageType)
    {
        bool shouldPersist = false;
        if (typeToPersistence.TryGetValue(messageType, out shouldPersist) == false)
        {
            shouldPersist = GetAndCachePersistenceFromAttribute(messageType);
        }
        return shouldPersist;
    }

    /// <summary>
    /// <see cref="ProjectionReplaySetting"/>
    /// </summary>
    /// <param name="messageType"></param>
    /// <returns></returns>
    public static bool IsProjectionReplayOrdered(this Type messageType)
    {
        bool shouldPersist = false;
        if (typeToOrder.TryGetValue(messageType, out shouldPersist) == false)
        {
            shouldPersist = GetAndCacheOrderFromAttribute(messageType);
        }
        return shouldPersist;
    }

    public static DateTimeOffset? GetProjectionAfterTimestamp(this Type messageType)
    {
        DateTimeOffset? afterTimestamp = null;
        if (typeToAfterTimestamp.TryGetValue(messageType, out afterTimestamp) == false)
        {
            afterTimestamp = GetAndCacheAfterTimestampFromAttribute(messageType);
        }
        return afterTimestamp;
    }

    /// <summary>
    /// <see cref="ProjectionEventsPersistenceSetting"/>
    /// </summary>
    /// <param name="messageType"></param>
    /// <returns></returns>
    private static bool GetAndCachePersistenceFromAttribute(Type type)
    {
        ProjectionAttribute contract = type
            .GetCustomAttributes(true).Where(attr => attr is ProjectionAttribute)
            .SingleOrDefault() as ProjectionAttribute;

        if (contract is null)
        {
            typeToPersistence.TryAdd(type, false);
            return false;
        }

        bool shouldPersist = false;
        if (contract.Persistence == ProjectionEventsPersistenceSetting.Persistent)
            shouldPersist = true;

        typeToPersistence.TryAdd(type, shouldPersist);
        return shouldPersist;
    }

    private static bool GetAndCacheOrderFromAttribute(Type type)
    {
        ProjectionAttribute contract = type
            .GetCustomAttributes(true).Where(attr => attr is ProjectionAttribute)
            .SingleOrDefault() as ProjectionAttribute;

        if (contract is null)
        {
            typeToOrder.TryAdd(type, false);
            return false;
        }

        bool isOrdered = false;
        if (contract.Order == ProjectionReplaySetting.Ordered)
            isOrdered = true;

        typeToOrder.TryAdd(type, isOrdered);
        return isOrdered;
    }

    private static DateTimeOffset? GetAndCacheAfterTimestampFromAttribute(Type type)
    {
        ProjectionAttribute contract = type
            .GetCustomAttributes(true).Where(attr => attr is ProjectionAttribute)
            .SingleOrDefault() as ProjectionAttribute;

        if (contract is null)
        {
            typeToAfterTimestamp.TryAdd(type, null);
            return null;
        }

        typeToAfterTimestamp.TryAdd(type, contract.Timestamp);
        return contract.Timestamp;
    }
}
