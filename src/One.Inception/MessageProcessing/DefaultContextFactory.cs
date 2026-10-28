using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using One.Inception.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace One.Inception.MessageProcessing;

/// <summary>
/// A factory for creating <see cref="HttpContext" /> instances.
/// </summary>
public class DefaultContextFactory
{
    private readonly ILogger<DefaultContextFactory> _logger;
    private readonly IInceptionContextAccessor _contextAccessor;
    private readonly ITenantResolver _tenantResolver;
    private TenantsOptions tenantsOptions;
    private readonly IOptionsMonitor<TenantsOptions> tenantsOptionsMonitor;

    public DefaultContextFactory(IServiceProvider serviceProvider, ILogger<DefaultContextFactory> logger)
    {
        // May be null
        _contextAccessor = serviceProvider.GetService<IInceptionContextAccessor>();
        _tenantResolver = serviceProvider.GetRequiredService<ITenantResolver>();
        _logger = logger;

        IOptionsMonitor<TenantsOptions> optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<TenantsOptions>>();
        tenantsOptionsMonitor = optionsMonitor;
        tenantsOptions = optionsMonitor.CurrentValue;

        optionsMonitor.OnChange(Changed);
    }

    internal IInceptionContextAccessor ContextAccessor => _contextAccessor;

    /// <summary>
    /// Create an <see cref="HttpContext"/> instance given an <paramref name="featureCollection" />.
    /// </summary>
    /// <param name="featureCollection"></param>
    /// <returns>An initialized <see cref="InceptionContext"/> object.</returns>
    public InceptionContext Create(object contextTarget, IServiceProvider contextServiceProvider)
    {
        ArgumentNullException.ThrowIfNull(contextTarget);

        string tenant = _tenantResolver.Resolve(contextTarget);
        EnsureValidTenant(tenant);

        InceptionContext context = new InceptionContext(tenant, contextServiceProvider, tenantsOptionsMonitor);

        _logger.LogTrace("Created tenant context: Tenant={tenant}, BeingDeleted={beingDeleted}", tenant, context.IsTenantBeingDeleted);
        Initialize(context);

        return context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Initialize(InceptionContext context)
    {
        Debug.Assert(context != null);

        if (_contextAccessor != null)
        {
            _contextAccessor.Context = context;
        }
    }

    private void EnsureValidTenant(string tenant)
    {
        if (string.IsNullOrEmpty(tenant)) throw new ArgumentNullException(nameof(tenant));

        if (tenantsOptions.Tenants.Where(t => t.Equals(tenant, StringComparison.OrdinalIgnoreCase)).Any() == false)
            throw new ArgumentException($"The tenant `{tenant}` is not registered. Make sure that the tenant `{tenant}` is properly configured using `Inception:tenants`. More info at https://github.com/1SoftwareCompany/1nception/blob/master/doc/Configuration.md", nameof(tenant));
    }

    private void Changed(TenantsOptions newOptions)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("tenants options re-loaded with {@options}", newOptions);

        tenantsOptions = newOptions;
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Tenant options refreshed. TenantsBeingDeleted={tenantsBeingDeleted}", string.Join(", ", newOptions.TenantsBeingDeleted ?? Array.Empty<string>()));
    }

    /// <summary>
    /// Clears the current <see cref="HttpContext" />.
    /// </summary>
    public void Dispose(InceptionContext context)
    {
        if (_contextAccessor != null)
        {
            _contextAccessor.Context = null;
        }
    }
}
