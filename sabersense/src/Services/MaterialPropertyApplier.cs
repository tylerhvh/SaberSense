// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Profiles;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Services;

internal static class MaterialPropertyApplier
{
    public static JToken? ResolveSplitValue(JToken? entry, SaberHand hand)
    {
        if (entry is null) return null;
        if (entry.Type != JTokenType.Object) return entry;
        var obj = (JObject)entry;
        if (!obj.ContainsKey("Left")) return entry;
        if (!obj.ContainsKey("Right")) return entry;
        return hand == SaberHand.Left ? obj["Left"] : obj["Right"];
    }

    public static bool IsSplit(JToken entry)
    {
        return entry is JObject obj && obj.ContainsKey("Left");
    }

    public static string? Apply(
        Material mat, int propId, ShaderPropertyType propType,
        JToken value, JsonSerializer json, TextureCacheRegistry? textureCache = null)
    {
        if (mat == null || value is null) return null;

        switch (propType)
        {
            case ShaderPropertyType.Float:
            case ShaderPropertyType.Range:
                mat.SetFloat(propId, value.ToObject<float>(json));
                break;

            case ShaderPropertyType.Color:
                mat.SetColor(propId, value.ToObject<Color>(json));
                break;

            case ShaderPropertyType.Vector:
                mat.SetVector(propId, value.ToObject<Vector4>(json));
                break;

            case ShaderPropertyType.Texture:
                var texName = value.ToObject<string>();
                if (string.IsNullOrEmpty(texName))
                {
                    mat.SetTexture(propId, null);
                }
                else
                {
                    var resolved = textureCache?.FindByName(texName!);
                    if (resolved != null)
                    {
                        mat.SetTexture(propId, resolved);
                    }
                    else if (texName!.Contains('\\') || texName.Contains('/'))
                    {
                        return texName;
                    }
                }
                break;
        }

        return null;
    }

    public static List<(int propId, string texName)>? ApplyAll(
        Material mat, JObject overrides, SaberHand hand,
        JsonSerializer json, TextureCacheRegistry? textureCache = null)
    {
        if (mat == null || overrides is null) return null;

        List<(int, string)>? asyncTextures = null;

        foreach (var prop in Core.Utilities.ShaderPropertyEnumerator.Enumerate(mat.shader))
        {
            var entry = overrides[prop.Name];
            if (entry is null) continue;

            var resolved = ResolveSplitValue(entry, hand);
            if (resolved is null) continue;

            var asyncTex = Apply(mat, prop.Id, prop.Type, resolved, json, textureCache);
            if (asyncTex is not null)
            {
                asyncTextures ??= [];
                asyncTextures.Add((prop.Id, asyncTex));
            }
        }

        return asyncTextures;
    }
}