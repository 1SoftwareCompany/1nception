﻿using One.Inception.EventStore.Index;
using One.Inception.FaultHandling;
using One.Inception.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace One.Inception.MessageProcessing;

/// <summary>
/// Work-flow which handles all events and writes them into the index
/// </summary>
public class EventStoreIndexSubscriberWorkflow<TIndex> : ISubscriberWorkflowFactory<TIndex>
    where TIndex : IEventStoreIndex
{
    private readonly IServiceProvider serviceProvider;

    public EventStoreIndexSubscriberWorkflow(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IWorkflow GetWorkflow()
    {
        ILogger<InMemoryRetryWorkflow<HandleContext>> logger = serviceProvider.GetRequiredService<ILogger<InMemoryRetryWorkflow<HandleContext>>>();

        MessageHandleWorkflow messageHandleWorkflow = new MessageHandleWorkflow(new CreateScopedHandlerWorkflow());
        messageHandleWorkflow.ActualHandle.Override(new DynamicMessageIndex());
        ScopedMessageWorkflow scopedWorkflow = new ScopedMessageWorkflow(messageHandleWorkflow, serviceProvider);
        InMemoryRetryWorkflow<HandleContext> retryableWorkflow = new InMemoryRetryWorkflow<HandleContext>(scopedWorkflow, logger);
        DiagnosticsWorkflow<HandleContext> diagnosticsWorkflow = new DiagnosticsWorkflow<HandleContext>(retryableWorkflow, serviceProvider.GetRequiredService<DiagnosticListener>(), serviceProvider.GetRequiredService<ActivitySource>());
        ExceptionEaterWorkflow<HandleContext> exceptionEater = new ExceptionEaterWorkflow<HandleContext>(diagnosticsWorkflow);

        return exceptionEater;
    }
}
