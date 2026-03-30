// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Threading.Tasks;

namespace SaberSense.Core.Utilities;

internal sealed class AsyncOnce
{
    private readonly object _lock = new();
    private Task? _task;
    private bool _completed;

    public Task RunOnceAsync(Func<Task> factory)
    {
        if (_completed) return Task.CompletedTask;

        Task taskToAwait;
        lock (_lock)
        {
            if (_completed) return Task.CompletedTask;
            _task ??= ExecuteAsync(factory);
            taskToAwait = _task;
        }

        return taskToAwait;
    }

    private async Task ExecuteAsync(Func<Task> factory)
    {
        try
        {
            await factory();
            lock (_lock)
            {
                _completed = true;
            }
        }
        catch
        {
            lock (_lock) { _task = null; }
            throw;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _task = null;
            _completed = false;
        }
    }

    public bool IsCompleted
    {
        get { lock (_lock) { return _completed; } }
    }
}