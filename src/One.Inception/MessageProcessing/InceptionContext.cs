using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using One.Inception.Multitenancy;

namespace One.Inception.MessageProcessing;

public sealed class InceptionContext
{
    private readonly IOptionsMonitor<TenantsOptions> tenantOptions;
    private readonly ILogger<InceptionContext> logger;

    public InceptionContext(string tenant, IServiceProvider serviceProvider, IOptionsMonitor<TenantsOptions> tenantOptions = null)
    {
        if (string.IsNullOrEmpty(tenant)) throw new ArgumentException($"Unknown tenant. InceptionContext is not properly built. Make sure that you have properly configured `Inception:tenants`. More info at https://github.com/1SoftwareCompany/1nception/blob/master/doc/Configuration.md");
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));

        this.tenantOptions = tenantOptions ?? serviceProvider.GetService<IOptionsMonitor<TenantsOptions>>();
        logger = serviceProvider.GetService<ILogger<InceptionContext>>();
        Tenant = tenant;
        ServiceProvider = serviceProvider;
        Trace = new Dictionary<string, object>();
    }

    public CancellationToken CancellationToken { get; internal set; }

    public string Tenant { get; private set; }

    public bool IsTenantBeingDeleted => tenantOptions?.CurrentValue.TenantsBeingDeleted?.Contains(Tenant, StringComparer.OrdinalIgnoreCase) == true;

    public void EnsureTenantIsNotBeingDeleted()
    {
        bool deleting = IsTenantBeingDeleted;
        logger?.LogTrace("Tenant schema creation check: Tenant={tenant}, BeingDeleted={beingDeleted}, Allowed={allowed}", Tenant, deleting, !deleting);
        if (deleting)
            throw new TenantBeingDeletedException(Tenant);
    }


    public IServiceProvider ServiceProvider { get; private set; }

    public Dictionary<string, object> Trace { get; }

    public bool IsNotInitialized => string.IsNullOrEmpty(Tenant) || ServiceProvider is null;

    public bool IsInitialized => IsNotInitialized == false;
}
