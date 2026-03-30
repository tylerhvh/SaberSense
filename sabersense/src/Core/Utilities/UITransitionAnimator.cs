// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Core.Utilities;

internal static class UITransitionAnimator
{
    public static async Task ScaleTransitionAsync(float duration, CancellationToken token, Action<float> onUpdate)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < duration)
        {
            if (token.IsCancellationRequested) return;
            float t = (Time.realtimeSinceStartup - start) / duration;
            try { onUpdate(t); }
            catch (MissingReferenceException) { return; }
            await Task.Yield();
        }
        if (!token.IsCancellationRequested)
        {
            try { onUpdate(1f); }
            catch (MissingReferenceException) {  }
        }
    }
}