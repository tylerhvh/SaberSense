// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIImage : UIElement
{
    public Image ImageComponent { get; private set; }

    public UIImage(string name = "Image") : base(name)
    {
        ImageComponent = GameObject.AddComponent<Image>();
        ImageComponent.type = Image.Type.Sliced;
        ImageComponent.material = UIMaterials.NoBloomMaterial;
        ImageComponent.raycastTarget = false;
    }

    public UIImage SetSprite(Sprite? sprite)
    {
        ImageComponent.sprite = sprite;
        return this;
    }

    public UIImage SetColor(Color color)
    {
        ImageComponent.color = color;
        return this;
    }

    public UIImage SetMaterial(Material mat)
    {
        ImageComponent.material = mat;
        return this;
    }
}