// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

internal sealed class VisibilityGroup
{
    private readonly GameObject _target;
    private readonly Dictionary<string, bool> _conditions = [];

    public VisibilityGroup(GameObject target)
    {
        _target = target;
    }

    public void SetCondition(string key, bool value)
    {
        _conditions[key] = value;
        Sync();
    }

    private void Sync()
    {
        if (_target == null) return;
        bool active = true;
        foreach (var kvp in _conditions)
        {
            if (!kvp.Value) { active = false; break; }
        }
        _target.SetActive(active);
    }
}