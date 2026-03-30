// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public static class UIBuilder
{
    public static T AddComponent<T>(this UIElement element) where T : Component
    {
        return element.GameObject.AddComponent<T>();
    }

    public static T GetComponent<T>(this UIElement element) where T : Component
    {
        return element.GameObject.GetComponent<T>();
    }

    public static T AddLayoutElement<T>(this T element, float minWidth = -1, float minHeight = -1, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1) where T : UIElement
    {
        LayoutElement le = element.GameObject.GetComponent<LayoutElement>() ?? element.GameObject.AddComponent<LayoutElement>();

        if (minWidth >= 0) le.minWidth = minWidth;
        if (minHeight >= 0) le.minHeight = minHeight;
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
        if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;

        return element;
    }

    public static T SetParent<T>(this T element, Transform parent, bool worldPositionStays = false) where T : UIElement
    {
        element.RectTransform.SetParent(parent, worldPositionStays);
        return element;
    }

    public static T SetParent<T>(this T element, UIElement parentElement, bool worldPositionStays = false) where T : UIElement
    {
        return element.SetParent(parentElement.RectTransform, worldPositionStays);
    }

    public static T SetActive<T>(this T element, bool active) where T : UIElement
    {
        element.GameObject.SetActive(active);
        return element;
    }

    public static T SetAnchors<T>(this T element, Vector2 min, Vector2 max) where T : UIElement
    {
        element.RectTransform.anchorMin = min;
        element.RectTransform.anchorMax = max;
        return element;
    }

    public static T SetSizeDelta<T>(this T element, Vector2 size) where T : UIElement
    {
        element.RectTransform.sizeDelta = size;
        return element;
    }

    public static T SetPivot<T>(this T element, Vector2 pivot) where T : UIElement
    {
        element.RectTransform.pivot = pivot;
        return element;
    }

    public static T SetAnchoredPosition<T>(this T element, Vector2 position) where T : UIElement
    {
        element.RectTransform.anchoredPosition = position;
        return element;
    }
}

public static class UIBorderUtils
{
    public static void DrawBorderLines(string prefix, RectTransform parent, Color color, float inset, float thickness, float textStartX, float textWidth)
    {
        var l = new UIImage(prefix + "L").SetColor(color).SetParent(parent, false);
        l.SetAnchors(new Vector2(0, 0), new Vector2(0, 1));
        l.RectTransform.offsetMin = new Vector2(inset, inset);
        l.RectTransform.offsetMax = new Vector2(inset + thickness, -inset);

        var r = new UIImage(prefix + "R").SetColor(color).SetParent(parent, false);
        r.SetAnchors(new Vector2(1, 0), new Vector2(1, 1));
        r.RectTransform.offsetMin = new Vector2(-inset - thickness, inset);
        r.RectTransform.offsetMax = new Vector2(-inset, -inset);

        var b = new UIImage(prefix + "B").SetColor(color).SetParent(parent, false);
        b.SetAnchors(new Vector2(0, 0), new Vector2(1, 0));
        b.RectTransform.offsetMin = new Vector2(inset + thickness, inset);
        b.RectTransform.offsetMax = new Vector2(-inset - thickness, inset + thickness);

        if (textWidth > 0 && textStartX >= 0)
        {
            var tl = new UIImage(prefix + "TL").SetColor(color).SetParent(parent, false);
            tl.SetAnchors(new Vector2(0, 1), new Vector2(0, 1));
            tl.RectTransform.offsetMin = new Vector2(inset + thickness, -inset - thickness);
            tl.RectTransform.offsetMax = new Vector2(textStartX, -inset);

            var tr = new UIImage(prefix + "TR").SetColor(color).SetParent(parent, false);
            tr.SetAnchors(new Vector2(0, 1), new Vector2(1, 1));
            tr.RectTransform.offsetMin = new Vector2(textStartX + textWidth, -inset - thickness);
            tr.RectTransform.offsetMax = new Vector2(-inset - thickness, -inset);
        }
        else
        {
            var t = new UIImage(prefix + "T").SetColor(color).SetParent(parent, false);
            t.SetAnchors(new Vector2(0, 1), new Vector2(1, 1));
            t.RectTransform.offsetMin = new Vector2(inset + thickness, -inset - thickness);
            t.RectTransform.offsetMax = new Vector2(-inset - thickness, -inset);
        }
    }
}