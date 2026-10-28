using System;
using System.Collections.Generic;
using System.Linq;
using Machine.Specifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using One.Inception.MessageProcessing;
using One.Inception.Multitenancy;

namespace One.Inception.Tests;

[Subject("Tenant deletion context")]
public class When_reading_the_tenant_deletion_option
{
    It observes_refreshes_in_an_existing_context = () =>
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var options = new ChangingTenantsOptions();
        var context = new InceptionContext("tenant", services, options);
        if (context.IsTenantBeingDeleted) throw new Exception("Unexpected deletion flag.");
        options.CurrentValue = new TenantsOptions { TenantsBeingDeleted = new[] { "tenant" } };
        if (!context.IsTenantBeingDeleted) throw new Exception("Existing context did not see refreshed options.");
        options.CurrentValue = new TenantsOptions();
        if (context.IsTenantBeingDeleted) throw new Exception("Context ignored an explicit reset.");
    };

    It leaves_other_tenants_unaffected = () =>
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var options = new ChangingTenantsOptions
        {
            CurrentValue = new TenantsOptions { TenantsBeingDeleted = new[] { "other" } }
        };
        if (new InceptionContext("tenant", services, options).IsTenantBeingDeleted)
            throw new Exception("Another tenant's flag affected this context.");
    };

    It binds_the_collection_and_matches_members_case_insensitively = () =>
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["inception:tenantsbeingdeleted:0"] = "tenant",
            ["inception:tenantsbeingdeleted:1"] = "other"
        }).Build();
        var options = new TenantsOptions();
        new TenantsOptionsProvider(config).Configure(options);
        if (!options.TenantsBeingDeleted.Contains("TENANT", StringComparer.OrdinalIgnoreCase) || !options.TenantsBeingDeleted.Contains("other"))
            throw new Exception("Tenant deletion collection was not bound correctly.");
    };

    It defaults_to_normal_access_without_options = () =>
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        if (new InceptionContext("tenant", services).IsTenantBeingDeleted)
            throw new Exception("Missing options blocked normal access.");
    };
}

internal sealed class ChangingTenantsOptions : IOptionsMonitor<TenantsOptions>
{
    public TenantsOptions CurrentValue { get; set; } = new TenantsOptions();
    public TenantsOptions Get(string name) => CurrentValue;
    public IDisposable OnChange(Action<TenantsOptions, string> listener) => null;
}