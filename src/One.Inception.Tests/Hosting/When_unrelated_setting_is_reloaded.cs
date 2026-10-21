using System.Collections.Generic;
using Machine.Specifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using One.Inception.Multitenancy;

namespace One.Inception.Hosting;

[Subject("InceptionOptionsProviderBase")]
public class When_unrelated_setting_is_reloaded
{
    Establish context = () =>
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["inception:tenants:0"] = "pruvit",
                ["inception:api:jwttenantconfig"] = "old"
            })
            .Build();

        provider = new TenantsOptionsProvider(configuration);
        ChangeToken.OnChange(provider.GetChangeToken, () => tenantsChanges++);
    };

    Because of = () =>
    {
        configuration["inception:api:jwttenantconfig"] = "new";
        configuration.Reload();
    };

    It should_not_notify_tenants_options_change = () => tenantsChanges.ShouldEqual(0);

    static IConfigurationRoot configuration;
    static TenantsOptionsProvider provider;
    static int tenantsChanges;
}

[Subject("InceptionOptionsProviderBase")]
public class When_tenants_setting_is_reloaded
{
    Establish context = () =>
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["inception:tenants:0"] = "pruvit"
            })
            .Build();

        provider = new TenantsOptionsProvider(configuration);
        ChangeToken.OnChange(provider.GetChangeToken, () => tenantsChanges++);
    };

    Because of = () =>
    {
        configuration["inception:tenants:1"] = "iskambira3";
        configuration.Reload();
        configuration.Reload();
    };

    It should_notify_tenants_options_change_once = () => tenantsChanges.ShouldEqual(1);

    static IConfigurationRoot configuration;
    static TenantsOptionsProvider provider;
    static int tenantsChanges;
}

[Subject("InceptionOptionsProviderBase")]
public class When_non_bootstrap_inception_settings_are_reloaded
{
    Establish context = () =>
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["inception:tenants:0"] = "pruvit",
                ["inception:api:jwttenantconfig"] = "old",
                ["inception:projectionsenabled"] = "true"
            })
            .Build();

        provider = new InceptionHostOptionsProvider(configuration);
        ChangeToken.OnChange(provider.GetChangeToken, () => hostChanges++);
    };

    Because of = () =>
    {
        configuration["inception:tenants:1"] = "iskambira3";
        configuration.Reload();
        configuration["inception:api:jwttenantconfig"] = "new";
        configuration.Reload();
        configuration["shopify:iskambira3"] = "x";
        configuration.Reload();
    };

    It should_not_notify_host_options_change = () => hostChanges.ShouldEqual(0);

    It should_notify_when_a_bootstrap_flag_changes = () =>
    {
        configuration["inception:projectionsenabled"] = "false";
        configuration.Reload();
        hostChanges.ShouldEqual(1);
    };

    static IConfigurationRoot configuration;
    static InceptionHostOptionsProvider provider;
    static int hostChanges;
}

[Subject("InceptionOptionsProviderBase")]
public class When_non_public_bound_setting_is_reloaded
{
    Establish context = () =>
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["inception:test:server"] = "rabbit",
                ["inception:test:externalservices:0:server"] = "old-rabbit"
            })
            .Build();

        provider = new NonPublicOptionsProvider(configuration);
        ChangeToken.OnChange(provider.GetChangeToken, () => changes++);
    };

    Because of = () =>
    {
        configuration["inception:test:externalservices:0:server"] = "new-rabbit";
        configuration.Reload();
    };

    It should_notify_options_change = () => changes.ShouldEqual(1);

    static IConfigurationRoot configuration;
    static NonPublicOptionsProvider provider;
    static int changes;

    public class NonPublicOptions
    {
        public string Server { get; set; }

        internal List<NonPublicOptions> ExternalServices { get; set; }
    }

    class NonPublicOptionsProvider : InceptionOptionsProviderBase<NonPublicOptions>
    {
        public NonPublicOptionsProvider(IConfiguration configuration) : base(configuration) { }

        public override void Configure(NonPublicOptions options)
        {
            configuration.GetSection("inception:test").Bind(options, opt => opt.BindNonPublicProperties = true);
        }
    }
}
