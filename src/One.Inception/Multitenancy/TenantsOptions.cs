﻿using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace One.Inception.Multitenancy;

public class TenantsOptions
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "The configuration `Inception:Tenants` is required. For more information see here https://github.com/1SoftwareCompany/1nception/blob/master/doc/Configuration.md")]
    [CollectionRegularExpression(@"^\b([\w\d_]+$)")]
    public IEnumerable<string> Tenants { get; set; }
}

public class TenantsOptionsProvider : InceptionOptionsProviderBase<TenantsOptions>
{
    public const string SettingKey = "inception:tenants";

    public TenantsOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(TenantsOptions options)
    {
        options.Tenants = configuration
            .GetSection(SettingKey).Get<string[]>()
            ?.Select(x => x.ToLower().Trim())
            ?.Distinct();
    }
}
