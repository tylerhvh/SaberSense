// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using TMPro;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public class UILabel : UIElement
{
    public TextMeshProUGUI TextComponent { get; private set; }

    public UILabel(string name = "Label", string text = "") : base(name)
    {
        TextComponent = GameObject.AddComponent<TextMeshProUGUI>();
        TextComponent.text = text;
        TextComponent.alignment = TextAlignmentOptions.Center;
        TextComponent.enableWordWrapping = false;
        TextComponent.richText = true;
        TextComponent.fontSize = UITheme.FontNormal;
        TextComponent.enableAutoSizing = true;
        TextComponent.fontSizeMin = 1f;
        TextComponent.fontSizeMax = UITheme.FontNormal;
        TextComponent.color = UITheme.TextPrimary;
    }

    public UILabel SetText(string text)
    {
        TextComponent.text = text;
        return this;
    }

    public UILabel SetColor(Color color)
    {
        TextComponent.color = color;
        return this;
    }

    public UILabel SetFontSize(float size)
    {
        TextComponent.fontSize = size;
        TextComponent.fontSizeMax = size;
        return this;
    }

    public UILabel SetAlignment(TextAlignmentOptions alignment)
    {
        TextComponent.alignment = alignment;
        return this;
    }
}