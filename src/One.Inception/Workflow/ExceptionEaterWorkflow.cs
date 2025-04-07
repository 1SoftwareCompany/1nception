﻿using System;
using System.Threading.Tasks;
using One.Inception.MessageProcessing;
using Microsoft.Extensions.Logging;

namespace One.Inception.Workflow;

public sealed class ExceptionEaterWorkflow<TContext> : Workflow<TContext> where TContext : HandleContext
{
    private static readonly ILogger logger = InceptionLogger.CreateLogger(typeof(DiagnosticsWorkflow<>));

    readonly Workflow<TContext> workflow;

    public ExceptionEaterWorkflow(Workflow<TContext> workflow)
    {
        this.workflow = workflow;
    }
    protected override async Task RunAsync(Execution<TContext> execution)
    {
        try { await workflow.RunAsync(execution.Context).ConfigureAwait(false); } // here we shouldn't remove async keyword 'cause it'll raise an exception outside this catch
        catch (Exception ex) when (True(() => logger.LogError(ex, "Somewhere along the way an exception was thrown and it was eaten. See inner exception"))) { }
    }
}
