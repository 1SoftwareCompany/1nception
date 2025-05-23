﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace One.Inception.EventStore;

public sealed class LoggingAggregateRepository : IAggregateRepository
{
    private readonly IAggregateRepository realDeal;
    private readonly ILogger<LoggingAggregateRepository> logger;

    public LoggingAggregateRepository(IAggregateRepository realDeal, ILogger<LoggingAggregateRepository> logger)
    {
        this.realDeal = realDeal;
        this.logger = logger;
    }

    public async Task<ReadResult<AR>> LoadAsync<AR>(AggregateRootId id) where AR : IAggregateRoot
    {
        using (logger.BeginScope(s => s
                                        .AddScope(Log.AggregateName, id.AggregateRootName)
                                        .AddScope(Log.AggregateId, id.Value)))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Loading aggregate...");

            return await realDeal.LoadAsync<AR>(id).ConfigureAwait(false);
        }
    }

    public async Task SaveAsync<AR>(AR aggregateRoot) where AR : IAggregateRoot
    {
        using (logger.BeginScope(s => s
                                        .AddScope(Log.AggregateName, typeof(AR).Name)
                                        .AddScope(Log.AggregateId, GetAggregateRootId(aggregateRoot))))
        {
            await realDeal.SaveAsync<AR>(aggregateRoot).ConfigureAwait(false);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Aggregate has been saved.");
        }
    }

    #region Welcome
    private string GetAggregateRootId<AR>(AR aggregateRoot) where AR : IAggregateRoot
    {
        string aggregateId = string.Empty;
        try { aggregateId = aggregateRoot.State.Id.Value; }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Unable to save aggregate. There is a problem with the ID for {typeof(AR).Name}");
            throw new Exception($"Unable to save aggregate. There is a problem with the ID for {typeof(AR).Name}. Check the inner exception if you want to be confused even more.", ex);
        }

        return aggregateId;
    }
    #endregion
}
