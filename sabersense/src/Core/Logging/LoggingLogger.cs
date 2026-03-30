// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.Logging;

internal sealed class LoggingLogger(IModLogger inner, params ILogSink[] sinks) : IModLogger
{
    private readonly IModLogger _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ILogSink[] _sinks = sinks ?? [];

    public void Debug(string message) { _inner.Debug(message); PushToSinks(LogLevel.Debug, null, message); }
    public void Info(string message) { _inner.Info(message); PushToSinks(LogLevel.Info, null, message); }
    public void Warn(string message) { _inner.Warn(message); PushToSinks(LogLevel.Warn, null, message); }
    public void Error(string message) { _inner.Error(message); PushToSinks(LogLevel.Error, null, message); }

    public void Error(Exception exception)
    {
        _inner.Error(exception);
        PushToSinks(LogLevel.Error, null, exception?.ToString() ?? "(null exception)");
    }

    public IModLogger ForSource(string source) => new ScopedLogger(this, source);

    internal void Log(LogLevel level, string? source, string message)
    {
        string ipaMessage = source is not null ? $"[{source}] {message}" : message;
        switch (level)
        {
            case LogLevel.Debug: _inner.Debug(ipaMessage); break;
            case LogLevel.Info: _inner.Info(ipaMessage); break;
            case LogLevel.Warn: _inner.Warn(ipaMessage); break;
            case LogLevel.Error: _inner.Error(ipaMessage); break;
        }

        PushToSinks(level, source, message);
    }

    private void PushToSinks(LogLevel level, string? source, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, source, message);
        for (int i = 0; i < _sinks.Length; i++)
        {
            try { _sinks[i].Push(entry); }
            catch {  }
        }
    }
}