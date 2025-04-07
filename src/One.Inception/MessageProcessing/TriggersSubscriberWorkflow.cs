using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;

namespace One.Inception.MessageProcessing;

public class TriggersSubscriberWorkflow<TTrigger> : ISubscriberWorkflowFactory<TTrigger>
{
    private readonly IServiceProvider serviceProvider;

    public TriggersSubscriberWorkflow(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IWorkflow GetWorkflow()
    {
        MessageHandleWorkflow messageHandleWorkflow = new MessageHandleWorkflow(new CreateScopedHandlerWorkflow());
        ScopedMessageWorkflow scopedWorkflow = new ScopedMessageWorkflow(messageHandleWorkflow, serviceProvider);
        DiagnosticsWorkflow<HandleContext> diagnosticsWorkflow = new DiagnosticsWorkflow<HandleContext>(scopedWorkflow, serviceProvider.GetRequiredService<DiagnosticListener>(), serviceProvider.GetRequiredService<ActivitySource>());

        return diagnosticsWorkflow;
    }
}
