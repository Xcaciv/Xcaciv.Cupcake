using Microsoft.Extensions.Logging;
using Xcaciv.Command.Interface;

namespace Xcaciv.Command.Packages.Abstractions;

/// <summary>
/// Logger factory that creates NuGetIoContextLogger instances
/// </summary>
public class NuGetIoContextLoggerFactory : ILoggerFactory
{
    private readonly Dictionary<string, NuGetIoContextLogger> loggers = new();
    private IIoContext ioContext;

    public void SetIoContext(IIoContext context)
    {
        this.ioContext = context;
        foreach (var logger in loggers.Values)
        {
            logger.SetIoContext(context);
        }
    }

    public void ClearIoContext()
    {
        foreach (var logger in loggers.Values)
        {
            logger.ClearIoContext();
        }
        this.ioContext = null;
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (!loggers.TryGetValue(categoryName, out var logger))
        {
            logger = new NuGetIoContextLogger(categoryName);
            if (ioContext is not null)
            {
                logger.SetIoContext(ioContext);
            }
            loggers[categoryName] = logger;
        }

        return logger;
    }

    public void AddProvider(ILoggerProvider provider)
    {
        // Not used in this implementation
    }

    public void Dispose()
    {
        loggers.Clear();
    }
}
