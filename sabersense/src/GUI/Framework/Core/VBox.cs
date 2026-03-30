// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class VBox : UIElement
{
    public VerticalLayoutGroup LayoutGroup { get; private set; }

    public VBox(string name = "VBox") : base(name)
    {
        LayoutGroup = GameObject.AddComponent<VerticalLayoutGroup>();
        LayoutGroup.childControlWidth = true;
        LayoutGroup.childControlHeight = true;
        LayoutGroup.childForceExpandWidth = true;
        LayoutGroup.childForceExpandHeight = false;
    }

    public VBox SetSpacing(float spacing)
    {
        LayoutGroup.spacing = spacing;
        return this;
    }

    public VBox SetPadding(int left, int right, int top, int bottom)
    {
        LayoutGroup.padding = new RectOffset(left, right, top, bottom);
        return this;
    }

    public VBox SetAlignment(TextAnchor anchor)
    {
        LayoutGroup.childAlignment = anchor;
        return this;
    }
}