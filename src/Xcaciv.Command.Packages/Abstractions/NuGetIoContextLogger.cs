using System;
using Microsoft.Extensions.Logging;
using Xcaciv.Command.Interface;

namespace Xcaciv.Command.Packages.Abstractions;

/// <summary>
/// Logger adapter that forwards NuGet logging messages to IIoContext via AddTraceMessage()
/// </summary>
public class NuGetIoContextLogger : ILogger
{
    private readonly string categoryName;
    private IIoContext ioContext;

    public NuGetIoContextLogger(string categoryName)
    {
        this.categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
    }

    public void SetIoContext(IIoContext context)
    {
        this.ioContext = context;
    }

    public void ClearIoContext()
    {
        this.ioContext = null;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return ioContext is not null && logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel) || ioContext is null)
        {
            return;
        }

        var message = formatter(state, exception);
        var logMessage = String.IsNullOrEmpty(message) ? String.Empty : message;

        if (exception is not null)
        {
            logMessage = $"{logMessage} | Exception: {exception.Message}";
        }

        ioContext.AddTraceMessage($"[{categoryName}] {logMessage}").ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}

/// <summary>
/// Generic logger wrapper for ILogger<T> that forwards messages to IIoContext
/// </summary>
public class NuGetIoContextLogger<T> : ILogger<T>
{
    private readonly NuGetIoContextLogger baseLogger;

    public NuGetIoContextLogger()
    {
        this.baseLogger = new NuGetIoContextLogger(typeof(T).Name);
    }

    public void SetIoContext(IIoContext context)
    {
        baseLogger.SetIoContext(context);
    }

    public void ClearIoContext()
    {
        baseLogger.ClearIoContext();
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return baseLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return baseLogger.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        baseLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}
