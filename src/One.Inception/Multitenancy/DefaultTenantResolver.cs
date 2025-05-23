﻿using System;
using System.Linq;
using One.Inception.EventStore;

namespace One.Inception.Multitenancy;

public class DefaultTenantResolver :
    ITenantResolver<string>,
    ITenantResolver<AggregateRootId>,
    ITenantResolver<AggregateCommit>,
    ITenantResolver<IMessage>,
    ITenantResolver<IBlobId>,
    ITenantResolver<InceptionMessage>
{
    //public string Resolve(ProjectionCommit projectionCommit)
    //{
    //    if (projectionCommit is null == true) throw new ArgumentNullException(nameof(projectionCommit));

    //    string tenant;
    //    if (TryResolve(projectionCommit.ProjectionId.RawId, out tenant))
    //        return tenant;

    //    throw new NotSupportedException($"Unable to resolve tenant for id {projectionCommit.ProjectionId}");
    //}

    public string Resolve(IBlobId id)
    {
        if (id is null == true) throw new ArgumentNullException(nameof(id));

        string tenant;
        if (TryResolve(id.RawId.Span, out tenant))
            return tenant;

        throw new NotSupportedException($"Unable to resolve tenant for id {id}");
    }

    public string Resolve(AggregateRootId id)
    {
        if (id is null == true) throw new ArgumentNullException(nameof(id));

        if (id is AggregateRootId)
            return ((AggregateRootId)id).Tenant;

        throw new NotSupportedException($"Unable to resolve tenant for id {id}");
    }

    public string Resolve(AggregateCommit aggregateCommit)
    {
        if (aggregateCommit is null == true) throw new ArgumentNullException(nameof(aggregateCommit));

        string tenant;
        if (TryResolve(aggregateCommit.AggregateRootId.Span, out tenant))
            return tenant;

        throw new NotSupportedException($"Unable to resolve tenant for id {aggregateCommit.AggregateRootId}");
    }

    public string Resolve(IMessage message)
    {
        var tenantPropertyMeta = message.GetType().GetProperty("Tenant", typeof(string));
        if (tenantPropertyMeta is null == false)
        {
            return (string)tenantPropertyMeta.GetValue(message);
        }

        var idMeta = message.GetType().GetProperties().Where(p => typeof(IBlobId).IsAssignableFrom(p.PropertyType)).FirstOrDefault();
        IBlobId id = idMeta?.GetValue(message) as IBlobId;
        if (id is null == false)
        {
            return Resolve(id);
        }

        throw new NotSupportedException($"Unable to resolve tenant from {message}");
    }

    public string Resolve(InceptionMessage message)
    {
        var tenant = message.GetTenant();
        if (string.IsNullOrEmpty(tenant))
        {
            return Resolve(message.Payload);
        }

        return tenant;
    }

    public string Resolve(string source)
    {
        return source;
    }

    bool TryResolve(ReadOnlySpan<byte> id, out string tenant)
    {
        tenant = string.Empty;
        var urn = System.Text.Encoding.UTF8.GetString(id);
        AggregateRootId aggregateUrn;

        if (AggregateRootId.TryParse(urn, out aggregateUrn))
        {
            tenant = aggregateUrn.Tenant;
            return true;
        }

        return false;
    }
}
