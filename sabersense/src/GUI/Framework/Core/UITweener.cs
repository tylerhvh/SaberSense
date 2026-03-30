// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public static class UITweener
{
    public static Coroutine? FadeColor(MonoBehaviour host, Graphic graphic, Color targetColor, float duration, Action? onComplete = null)
    {
        if (host == null || !host.gameObject.activeInHierarchy || graphic == null) return null;
        return host.StartCoroutine(FadeColorRoutine(graphic, targetColor, duration, onComplete));
    }

    private static IEnumerator FadeColorRoutine(Graphic graphic, Color targetColor, float duration, Action? onComplete)
    {
        if (graphic == null) yield break;

        Color startColor = graphic.color;
        float time = 0f;

        while (time < duration)
        {
            if (graphic == null) yield break;

            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);

            graphic.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        if (graphic != null) graphic.color = targetColor;
        onComplete?.Invoke();
    }

    public static Coroutine? AnchorHeight(MonoBehaviour host, RectTransform rect, float targetHeight, float duration, Action? onComplete = null)
    {
        if (host == null || !host.gameObject.activeInHierarchy || rect == null) return null;
        return host.StartCoroutine(AnchorHeightRoutine(rect, targetHeight, duration, onComplete));
    }

    private static IEnumerator AnchorHeightRoutine(RectTransform rect, float targetHeight, float duration, Action? onComplete)
    {
        if (rect == null) yield break;

        Vector2 startSize = rect.sizeDelta;
        float time = 0f;

        while (time < duration)
        {
            if (rect == null) yield break;

            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);

            rect.sizeDelta = new Vector2(startSize.x, Mathf.Lerp(startSize.y, targetHeight, t));
            yield return null;
        }

        if (rect != null) rect.sizeDelta = new Vector2(startSize.x, targetHeight);
        onComplete?.Invoke();
    }
}