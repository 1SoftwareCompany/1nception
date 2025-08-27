using One.Inception.FaultHandling;
using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace One.Inception.MessageProcessing;

public class ApplicationServiceSubscriberWorkflow : ISubscriberWorkflowFactory<IApplicationService>
{
    private readonly IServiceProvider serviceProvider;

    public ApplicationServiceSubscriberWorkflow(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IWorkflow GetWorkflow()
    {
        ILogger<ApplicationServiceSubscriberWorkflow> logger = serviceProvider.GetRequiredService<ILogger<ApplicationServiceSubscriberWorkflow>>();
        RetryStrategyFactory fact = serviceProvider.GetRequiredService<RetryStrategyFactory>();

        MessageHandleWorkflow messageHandleWorkflow = new MessageHandleWorkflow(new CreateScopedHandlerWorkflow());
        ScopedMessageWorkflow scopedWorkflow = new ScopedMessageWorkflow(messageHandleWorkflow, serviceProvider);
        InMemoryRetryWorkflow<HandleContext> retryableWorkflow = new InMemoryRetryWorkflow<HandleContext>(scopedWorkflow, fact, logger);
        DiagnosticsWorkflow<HandleContext> diagnosticsWorkflow = new DiagnosticsWorkflow<HandleContext>(retryableWorkflow, serviceProvider.GetRequiredService<DiagnosticListener>(), serviceProvider.GetRequiredService<ActivitySource>());
        ExceptionEaterWorkflow<HandleContext> exceptionEater = new ExceptionEaterWorkflow<HandleContext>(diagnosticsWorkflow);

        return exceptionEater;
    }
}
