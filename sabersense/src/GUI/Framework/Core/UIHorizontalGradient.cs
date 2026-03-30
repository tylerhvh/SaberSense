// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

[RequireComponent(typeof(Graphic))]
public class UIHorizontalGradient : BaseMeshEffect
{
    private Color _colorLeft = Color.white;
    private Color _colorRight = Color.white;

    public Color ColorLeft
    {
        get => _colorLeft;
        set { if (_colorLeft != value) { _colorLeft = value; graphic?.SetVerticesDirty(); } }
    }

    public Color ColorRight
    {
        get => _colorRight;
        set { if (_colorRight != value) { _colorRight = value; graphic?.SetVerticesDirty(); } }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        float leftX = float.MaxValue;
        float rightX = float.MinValue;

        UIVertex vert = new();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vert, i);
            if (vert.position.x < leftX) leftX = vert.position.x;
            if (vert.position.x > rightX) rightX = vert.position.x;
        }

        float width = rightX - leftX;

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vert, i);

            float t = width > 0 ? (vert.position.x - leftX) / width : 0f;

            Color32 color = Color32.Lerp(ColorLeft, ColorRight, t);

            color.r = (byte)((color.r * vert.color.r) / 255);
            color.g = (byte)((color.g * vert.color.g) / 255);
            color.b = (byte)((color.b * vert.color.b) / 255);
            color.a = (byte)((color.a * vert.color.a) / 255);

            vert.color = color;
            vh.SetUIVertex(vert, i);
        }
    }
}