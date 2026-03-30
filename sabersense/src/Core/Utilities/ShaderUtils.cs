// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Core.Utilities;

internal static class ShaderUtils
{
    internal static readonly int TintColorId = Shader.PropertyToID("_Color");
    internal static readonly int BaseTextureId = Shader.PropertyToID("_MainTex");
    internal static readonly int CustomColorToggleId = Shader.PropertyToID("_CustomColors");
    internal static readonly int GlowIntensityId = Shader.PropertyToID("_Glow");
    internal static readonly int BloomIntensityId = Shader.PropertyToID("_Bloom");
    internal static readonly int LeftHandColorId = Shader.PropertyToID("_UserColorLeft");
    internal static readonly int RightHandColorId = Shader.PropertyToID("_UserColorRight");
    internal static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    internal static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    [System.ThreadStatic]
    private static MaterialPropertyBlock? _colorBlock;

    public static bool TryGetTexture(this Material material, int propId, out Texture? tex)
    {
        tex = material.HasProperty(propId) ? material.GetTexture(propId) : null;
        return tex != null;
    }

    public static bool TryGetMainTexture(this Material material, out Texture tex) =>
        TryGetTexture(material, ShaderUtils.BaseTextureId, out tex!);

    public static bool TryGetFloat(this Material material, int propId, out float val)
    {
        if (material.HasProperty(propId))
        {
            val = material.GetFloat(propId);
            return true;
        }
        val = 0f;
        return false;
    }

    public static bool SupportsSaberColoring(Material material)
    {
        if (material is null || !material.HasProperty(TintColorId)) return false;

        if (material.HasProperty(CustomColorToggleId))
            return material.GetFloat(CustomColorToggleId) > 0;

        return (material.HasProperty(GlowIntensityId) && material.GetFloat(GlowIntensityId) > 0)
            || (material.HasProperty(BloomIntensityId) && material.GetFloat(BloomIntensityId) > 0);
    }

    public static MaterialPropertyBlock ColorBlock(Color color)
    {
        _colorBlock ??= new();
        _colorBlock.SetColor(ShaderUtils.TintColorId, color);
        return _colorBlock;
    }

    public readonly record struct ShaderPropertyValue(
        object? Value, int PropertyId, ShaderPropertyType PropertyType);

    public static IReadOnlyList<ShaderPropertyValue> ReadProperties(
        this Material material, string? excludeTag = null)
    {
        var shader = material.shader;
        var count = shader.GetPropertyCount();
        var results = new List<ShaderPropertyValue>(count);

        for (var i = 0; i < count; i++)
        {
            if (!string.IsNullOrEmpty(excludeTag) &&
                shader.GetPropertyAttributes(i).Contains(excludeTag))
                continue;

            var id = shader.GetPropertyNameId(i);
            var type = shader.GetPropertyType(i);
            results.Add(new ShaderPropertyValue(material.GetProperty(id, type), id, type));
        }
        return results;
    }

    public static object? GetProperty(this Material material, int id, ShaderPropertyType type) => type switch
    {
        ShaderPropertyType.Color => material.GetColor(id),
        ShaderPropertyType.Vector => material.GetVector(id),
        ShaderPropertyType.Float or ShaderPropertyType.Range => material.GetFloat(id),
        ShaderPropertyType.Texture => material.GetTexture(id),
        _ => null
    };

    public static void SetProperty(this Material material, int id, object obj, ShaderPropertyType type)
    {
        switch (type)
        {
            case ShaderPropertyType.Color when obj is Color c: material.SetColor(id, c); break;
            case ShaderPropertyType.Vector when obj is Vector2 v2: material.SetVector(id, v2); break;
            case ShaderPropertyType.Vector when obj is Vector3 v3: material.SetVector(id, v3); break;
            case ShaderPropertyType.Vector when obj is Vector4 v4: material.SetVector(id, v4); break;
            case ShaderPropertyType.Float or ShaderPropertyType.Range when obj is float f: material.SetFloat(id, f); break;
            case ShaderPropertyType.Texture when obj is Texture t: material.SetTexture(id, t); break;
        }
    }
}