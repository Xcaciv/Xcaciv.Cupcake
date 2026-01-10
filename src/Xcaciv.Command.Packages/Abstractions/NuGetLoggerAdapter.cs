namespace Xcaciv.Command.Packages.Abstractions;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetLogger = NuGet.Common.ILogger;
using NuGetLogLevel = NuGet.Common.LogLevel;

public class NuGetLoggerAdapter : NuGetLogger
{
    private readonly ILogger logger;

    public NuGetLoggerAdapter(ILogger logger)
    {
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public void Log(NuGetLogLevel level, string data)
    {
        switch (level)
        {
            case NuGetLogLevel.Debug:
                logger.LogDebug(data);
                break;
            case NuGetLogLevel.Verbose:
            case NuGetLogLevel.Information:
                logger.LogInformation(data);
                break;
            case NuGetLogLevel.Minimal:
            case NuGetLogLevel.Warning:
                logger.LogWarning(data);
                break;
            case NuGetLogLevel.Error:
                logger.LogError(data);
                break;
        }
    }

    public void Log(NuGet.Common.ILogMessage message)
    {
        if (message is null)
        {
            return;
        }

        Log(message.Level, message.Message);
    }

    public Task LogAsync(NuGetLogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }

    public Task LogAsync(NuGet.Common.ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    public void LogDebug(string data)
    {
        logger.LogDebug(data);
    }

    public void LogError(string data)
    {
        logger.LogError(data);
    }

    public void LogInformation(string data)
    {
        logger.LogInformation(data);
    }

    public void LogInformationSummary(string data)
    {
        logger.LogInformation(data);
    }

    public void LogMinimal(string data)
    {
        logger.LogInformation(data);
    }

    public void LogVerbose(string data)
    {
        logger.LogDebug(data);
    }

    public void LogWarning(string data)
    {
        logger.LogWarning(data);
    }
}
