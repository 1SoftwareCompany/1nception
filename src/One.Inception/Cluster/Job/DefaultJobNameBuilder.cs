using One.Inception.MessageProcessing;
using Microsoft.Extensions.Options;

namespace One.Inception.Cluster.Job;

public class DefaultJobNameBuilder : IJobNameBuilder
{
    private readonly BoundedContext boundedContext;
    private readonly IInceptionContextAccessor contextAccessor;

    public DefaultJobNameBuilder(IOptions<BoundedContext> boundedContext, IInceptionContextAccessor contextAccessor)
    {
        this.boundedContext = boundedContext.Value;
        this.contextAccessor = contextAccessor;
    }

    public string GetJobName(string defaultName)
    {
        return $"urn:{boundedContext.Name}:{contextAccessor.Context.Tenant}:{defaultName}";
    }
}
