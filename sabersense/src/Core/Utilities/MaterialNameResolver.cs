// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Core.Utilities;

internal sealed class MaterialNameResolver
{
    private readonly Dictionary<string, int> _seenNames = [];

    public string Resolve(Material mat)
    {
        if (mat == null) return "null";
        return Resolve(mat.name);
    }

    public string Resolve(string rawName)
    {
        var baseName = StripInstanceSuffix(rawName);

        if (_seenNames.TryGetValue(baseName, out var count))
        {
            _seenNames[baseName] = count + 1;
            return $"{baseName} ({count + 1})";
        }

        _seenNames[baseName] = 1;
        return baseName;
    }

    public static string StripInstanceSuffix(string name)
    {
        if (name is null) return string.Empty;
        const string suffix = " (Instance)";
        var idx = name.IndexOf(suffix);
        return idx >= 0 ? name.Remove(idx, suffix.Length) : name;
    }

    public static string StripUnityNameSuffixes(string name)
    {
        name = StripInstanceSuffix(name);
        const string cloneSuffix = " (Clone)";
        var idx = name.IndexOf(cloneSuffix);
        if (idx >= 0) name = name.Remove(idx, cloneSuffix.Length);
        return name.Trim();
    }

    public MaterialNameResolver BeginScope()
    {
        _seenNames.Clear();
        return this;
    }

    public void Reset() => _seenNames.Clear();
}