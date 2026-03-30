// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;

namespace SaberSense.GUI.Framework.Core;

public class UIBinding<T> : IDisposable
{
    private readonly Func<T> _getter;
    private readonly Action<T> _setter;
    private T _lastValue;
    private bool _disposed;

    public UIBinding(Func<T> getter, Action<T> setter)
    {
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        _lastValue = getter();
    }

    public void Push(T value)
    {
        if (_disposed) return;
        _setter(value);
        _lastValue = value;
    }

    public bool Pull(out T value)
    {
        if (_disposed) { value = default!; return false; }
        value = _getter();
        if (EqualityComparer<T>.Default.Equals(value, _lastValue)) return false;
        _lastValue = value;
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}