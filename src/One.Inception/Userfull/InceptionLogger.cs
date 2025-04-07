using System;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace One.Inception;

public static class Log
{
    public const string Tenant = "inception_tenant";
    public const string AggregateId = "inception_arid";
    public const string AggregateName = "inception_arname";

    public const string MessageId = "inception_messageId";
    public const string MessageData = "inception_messageData";
    public const string MessageType = "inception_messageType";

    public const string MessageHandler = "inception_messageHandler";

    public const string JobName = "inception_job_name";
    public const string JobData = "inception_job_data";

    public const string ProjectionName = "inception_projection_name";
    public const string ProjectionType = "inception_projection_type";
    public const string ProjectionInstanceId = "inception_projection_id";
    public const string ProjectionVersion = "inception_projection_version";
}

public static class InceptionLogger
{
    private static ILoggerFactory factory = new LoggerFactory();
    private static ILogger startupLogger;

    public static IHostBuilder UseInceptionStartupLogger(this IHostBuilder hostBuilder, ILogger startupLogger)
    {
        SetStartupLogger(startupLogger);
        return hostBuilder;
    }

    public static void SetStartupLogger(ILogger startupLogger) => InceptionLogger.startupLogger = startupLogger;

    internal static void Configure(ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null && startupLogger is null == false)
            startupLogger.LogWarning($"An instance of {nameof(ILoggerFactory)} is nowhere to be found. Casting blinding spell... To break the spell, you must configure your application logging.");

        factory = loggerFactory ?? factory;
        startupLogger = null;
    }

    public static ILogger CreateLogger<T>() => startupLogger ?? factory.CreateLogger<T>();
    public static ILogger CreateLogger(Type type) => startupLogger ?? factory.CreateLogger(type);
    public static ILogger CreateLogger(string categoryName) => startupLogger ?? factory.CreateLogger(categoryName);

    public static IDisposable BeginScope(this ILogger logger, Action<Dictionary<string, object>> scope)
    {
        var loggerScope = new Dictionary<string, object>();
        scope(loggerScope);
        return logger.BeginScope(loggerScope);
    }

    public static Dictionary<string, object> AddScope(this Dictionary<string, object> scope, string key, object value)
    {
        scope.Add(key, value);
        return scope;
    }
}
