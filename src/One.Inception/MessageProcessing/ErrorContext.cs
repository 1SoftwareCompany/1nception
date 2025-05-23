﻿using Microsoft.Extensions.DependencyInjection;
using System;

namespace One.Inception.MessageProcessing;

public interface IWorkflowContextWithServiceProvider
{
    IServiceProvider ServiceProvider { get; set; }
}

public class ErrorContext : IWorkflowContextWithServiceProvider
{
    public ErrorContext(Exception error, InceptionMessage message, Type handlerType)
    {
        Error = error;
        Message = message;
        HandlerType = handlerType;
    }

    public Exception Error { get; private set; }

    public InceptionMessage Message { get; private set; }

    public Type HandlerType { get; private set; }

    public IServiceProvider ServiceProvider { get; set; }

    public string MessageAsJson { get; private set; }

    public Exception ToException()
    {
        string errorMessage = $"MessageHandleWorkflow execution has failed.{Environment.NewLine}{GetMessageAsJson(ServiceProvider, Message)}";
        return new WorkflowExecutionException(errorMessage, this, Error);
    }

    private string GetMessageAsJson(IServiceProvider serviceProvider, InceptionMessage message)
    {
        if (string.IsNullOrEmpty(MessageAsJson) && ServiceProvider is not null)
        {
            try
            {
                var serializer = serviceProvider.GetRequiredService<ISerializer>();
                MessageAsJson = serializer.SerializeToString(message);
            }
            catch (Exception) { }
        }

        return MessageAsJson;
    }
}
