// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Core.Utilities;

internal readonly record struct RawShaderProperty(string Name, int Id, ShaderPropertyType Type);

internal static class ShaderPropertyEnumerator
{
    public static IEnumerable<RawShaderProperty> Enumerate(Shader shader)
    {
        if (shader == null) yield break;
        var count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            yield return new RawShaderProperty(
                shader.GetPropertyName(i),
                shader.GetPropertyNameId(i),
                shader.GetPropertyType(i));
        }
    }
}