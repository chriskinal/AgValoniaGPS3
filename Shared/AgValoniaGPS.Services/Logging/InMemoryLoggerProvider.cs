// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.Services.Logging;

/// <summary>
/// A log entry captured by the in-memory logger.
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Stores log entries in memory for the log viewer dialog.
/// Thread-safe; capped at MaxEntries to prevent unbounded growth.
/// </summary>
public sealed class LogStore
{
    public const int MaxEntries = 2000;

    private static readonly Lazy<LogStore> _instance = new(() => new LogStore());
    public static LogStore Instance => _instance.Value;

    private readonly object _lock = new();
    private readonly List<LogEntry> _entries = new(MaxEntries);

    public event Action? LogAdded;

    public void Add(LogEntry entry)
    {
        lock (_lock)
        {
            if (_entries.Count >= MaxEntries)
                _entries.RemoveAt(0);
            _entries.Add(entry);
        }
        LogAdded?.Invoke();
    }

    public List<LogEntry> GetSnapshot()
    {
        lock (_lock)
            return new List<LogEntry>(_entries);
    }

    public void Clear()
    {
        lock (_lock)
            _entries.Clear();
        LogAdded?.Invoke();
    }
}

/// <summary>
/// Logger that writes to the shared LogStore for display in the log viewer.
/// </summary>
internal sealed class InMemoryLogger : ILogger
{
    private readonly string _category;

    public InMemoryLogger(string category) => _category = category;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception != null)
            message += Environment.NewLine + exception;

        LogStore.Instance.Add(new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = logLevel,
            Category = _category,
            Message = message
        });
    }
}

/// <summary>
/// Logger provider that creates InMemoryLogger instances.
/// Register in DI to capture all log output for the log viewer.
/// </summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName);
    public void Dispose() { }
}
