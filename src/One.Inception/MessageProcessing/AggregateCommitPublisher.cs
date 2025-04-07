﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using One.Inception.EventStore;
using Microsoft.Extensions.Logging;

namespace One.Inception.MessageProcessing;

/// <summary>
/// Publishes live aggregate commits
/// </summary>
internal sealed class AggregateCommitPublisher : IAggregateCommitInterceptor
{
    private readonly IInceptionContextAccessor contextAccessor;
    private readonly IPublisher<AggregateCommit> publisher;
    private readonly ILogger<AggregateCommitPublisher> logger;

    public AggregateCommitPublisher(IPublisher<AggregateCommit> publisher, IInceptionContextAccessor contextAccessor, ILogger<AggregateCommitPublisher> logger)
    {
        this.publisher = publisher;
        this.contextAccessor = contextAccessor;
        this.logger = logger;
    }

    public Task OnAppendAsync(AggregateCommit origin)
    {
        try
        {
            bool publishResult = publisher.Publish(origin, BuildHeaders(origin));

            if (publishResult == false)
                logger.LogError("Unable to publish aggregate commit.");
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Unable to publish aggregate commit."))) { }

        return Task.CompletedTask;
    }

    public Task<AggregateCommit> OnAppendingAsync(AggregateCommit origin) => Task.FromResult(origin);

    Dictionary<string, string> BuildHeaders(AggregateCommit commit)
    {
        Dictionary<string, string> messageHeaders = new Dictionary<string, string>
        {
            { MessageHeader.AggregateRootId, Convert.ToBase64String(commit.AggregateRootId.Span) }
        };

        foreach (var trace in contextAccessor.Context.Trace)
        {
            messageHeaders.Add(trace.Key, trace.Value.ToString());
        }

        return messageHeaders;
    }
}
