using System;

namespace One.Inception.Multitenancy;

public sealed class TenantBeingDeletedException : InvalidOperationException
{
    public TenantBeingDeletedException(string tenant)
        : base($"Normal Cassandra access is blocked because tenant '{tenant}' is being deleted.") { }
}