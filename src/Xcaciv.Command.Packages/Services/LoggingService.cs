namespace Xcaciv.Command.Packages.Services;

using System;
using Microsoft.Extensions.Logging;

public class LoggingService
{
    private readonly ILoggerFactory loggerFactory;

    public LoggingService(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public ILogger CreateLogger(string categoryName) => this.loggerFactory.CreateLogger(categoryName);
}
