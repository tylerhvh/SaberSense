// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class HBox : UIElement
{
    public HorizontalLayoutGroup LayoutGroup { get; private set; }

    public HBox(string name = "HBox") : base(name)
    {
        LayoutGroup = GameObject.AddComponent<HorizontalLayoutGroup>();
        LayoutGroup.childControlWidth = true;
        LayoutGroup.childControlHeight = true;
        LayoutGroup.childForceExpandWidth = false;
        LayoutGroup.childForceExpandHeight = true;
    }

    public HBox SetSpacing(float spacing)
    {
        LayoutGroup.spacing = spacing;
        return this;
    }

    public HBox SetPadding(int left, int right, int top, int bottom)
    {
        LayoutGroup.padding = new RectOffset(left, right, top, bottom);
        return this;
    }

    public HBox SetAlignment(TextAnchor anchor)
    {
        LayoutGroup.childAlignment = anchor;
        return this;
    }
}