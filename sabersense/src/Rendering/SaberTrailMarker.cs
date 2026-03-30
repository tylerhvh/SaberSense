// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Rendering;

[DisallowMultipleComponent]
public sealed class SaberTrailMarker : MonoBehaviour
{
    public Transform? PointStart;
    public Transform? PointEnd;
    public Material? TrailMaterial;
    public TrailColorMode ColorMode = TrailColorMode.CustomColor;
    public Color TrailColor = Color.white;
    public Color MultiplierColor = Color.white;
    public int Length = 20;
}