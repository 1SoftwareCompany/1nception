using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace One.Inception;

public class BoundedContext
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "The configuration `Inception:BoundedContext` is required. For more information see here https://github.com/1SoftwareCompany/1nception/blob/master/doc/Configuration.md")]
    [RegularExpression(@"^\b([\w\d_]+$)", ErrorMessage = "Characters are not allowed for configuration `Inception:BoundedContext`. For more information see here https://github.com/1SoftwareCompany/1nception/blob/master/doc/Configuration.md")]
    public string Name { get; set; }

    public override string ToString() => Name;
}

public class BoundedContextProvider : InceptionOptionsProviderBase<BoundedContext>
{
    public const string SettingKey = "Inception:boundedcontext";

    public BoundedContextProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(BoundedContext options)
    {
        options.Name = configuration[SettingKey]?.ToLower()?.Trim();
    }
}
