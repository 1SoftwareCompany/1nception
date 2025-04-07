﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Cluster.Job;

public abstract class InceptionJob<TData> : IInceptionJob<TData>
    where TData : class, IJobData, new()
{
    Func<TData, TData> DataOverride = fromCluster => fromCluster;
    Func<TData> InitialDataFactory = () => new TData();

    protected readonly ILogger<InceptionJob<TData>> logger;

    public InceptionJob(ILogger<InceptionJob<TData>> logger)
    {
        this.logger = logger;
    }

    public abstract string Name { get; set; }

    /// <summary>
    /// Initializes a default state for the job
    /// </summary>
    /// <returns>Returns the state data</returns>
    private TData BuildInitialData()
    {
        var initialData = InitialDataFactory();
        OverrideData(cluster => Override(cluster, initialData));

        return initialData;
    }

    public TData BuildInitialData(Func<TData> factory)
    {
        InitialDataFactory = factory;

        return BuildInitialData();
    }

    public async Task<JobExecutionStatus> RunAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        try
        {
            using (logger.BeginScope(s => s.AddScope("inception_job_name", Name)))
            {
                logger.LogInformation("Initializing job...");

                await SyncInitialStateAsync(cluster, cancellationToken).ConfigureAwait(false);
                return await RunJobWithLoggerAsync(cluster, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "A job run has failed!")))
        {
            return JobExecutionStatus.Failed;
        }
    }

    public async Task SyncInitialStateAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        try
        {
            Data = await cluster.PingAsync<TData>(cancellationToken).ConfigureAwait(false);

            if (Data is null)
                Data = BuildInitialData();

            Data = DataOverride(Data);

            using (logger.BeginScope(s => s
                       .AddScope(Log.JobData, System.Text.Json.JsonSerializer.Serialize<TData>(Data))))
            {
                logger.LogInformation("Job state synced.");
            }
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Error while syncing initial state of a job."))) { }
    }

    private Task<JobExecutionStatus> RunJobWithLoggerAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        try
        {
            using (logger.BeginScope(s => s
                    .AddScope("inception_job_data", System.Text.Json.JsonSerializer.Serialize<TData>(Data))))
            {
                logger.LogInformation("A job has started...");
                return RunJobAsync(cluster, cancellationToken);
            }
        }
        catch (Exception ex) when (True(() => logger.LogError(ex, "Error on RunJobWithLoggerAsync")))
        {
            return Task.FromResult(JobExecutionStatus.Failed);
        }
    }

    protected abstract Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default);

    public TData Data { get; protected set; }

    public void OverrideData(Func<TData, TData> dataOverride)
    {
        DataOverride = dataOverride;
    }

    protected virtual TData Override(TData fromCluster, TData fromLocal)
    {
        if (fromCluster.IsCompleted && fromCluster.Timestamp < fromLocal.Timestamp)
            return fromLocal;
        else
            return fromCluster;
    }

    public virtual Task BeforeRunAsync() { return Task.CompletedTask; }

    public virtual Task AfterRunAsync() { return Task.CompletedTask; }
}
