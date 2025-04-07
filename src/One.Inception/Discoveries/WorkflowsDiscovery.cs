using System;
using System.Collections.Generic;
using One.Inception.MessageProcessing;
using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.Discoveries;

public interface IApiAccessor
{
    IServiceProvider Provider { get; set; }
}

public class ApiAccessor : IApiAccessor
{
    public IServiceProvider Provider { get; set; }
}

public class WorkflowsDiscovery : DiscoveryBase<IWorkflow>
{
    protected override DiscoveryResult<IWorkflow> DiscoverFromAssemblies(DiscoveryContext context)
    {
        return new DiscoveryResult<IWorkflow>(DiscoverWorkflows(context), RegisterGG);
    }

    protected virtual IEnumerable<DiscoveredModel> DiscoverWorkflows(DiscoveryContext context)
    {
        return DiscoverModel<Workflow<HandleContext>, MessageHandleWorkflow>(ServiceLifetime.Transient);
    }

    public void RegisterGG(IServiceCollection services)
    {
        services.AddSingleton<IApiAccessor, ApiAccessor>();
    }
}
