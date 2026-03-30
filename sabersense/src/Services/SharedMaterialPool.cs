// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Services;

internal sealed class SharedMaterialPool : IDisposable
{
    private readonly Dictionary<string, Material> _pool = [];
    private readonly List<Material> _owned = [];

    public Material GetOrClone(string resolvedName, Material original, SaberHand hand)
    {
        string key = $"{resolvedName}_{hand}";
        if (_pool.TryGetValue(key, out var existing))
            return existing;

        var clone = new Material(original);
        _pool[key] = clone;
        _owned.Add(clone);
        return clone;
    }

    public Material? Get(string resolvedName, SaberHand hand)
        => _pool.TryGetValue($"{resolvedName}_{hand}", out var mat) ? mat : null;

    public bool Contains(string resolvedName, SaberHand hand)
        => _pool.ContainsKey($"{resolvedName}_{hand}");

    public void Clear()
    {
        foreach (var mat in _owned)
            mat.TryDestroy();
        _pool.Clear();
        _owned.Clear();
    }

    public void Dispose() => Clear();
}