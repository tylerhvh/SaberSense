// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Catalog.Data;

internal static class JsonExtensions
{
    public static void Populate<T>(this JToken token, T target, JsonSerializer serializer)
    {
        using var reader = token.CreateReader();
        serializer.Populate(reader, target!);
    }
}

public class Serializer : IJsonProvider
{
    public JsonSerializer Json { get; } = CreateSerializer();

    private readonly SaberCatalog _catalog;
    private readonly ShaderIntrospector _shaders;
    private readonly TextureCacheRegistry _textures;

    internal Serializer(SaberCatalog catalog, ShaderIntrospector shaders, TextureCacheRegistry textures)
    {
        _catalog = catalog;
        _shaders = shaders;
        _textures = textures;
    }

    public async Task<SaberAssetEntry> ResolveSaberEntry(string relativePath)
    {
        await _catalog.WaitForFinish();
        return (await _catalog[relativePath])!;
    }

    public JToken SerializeMaterial(Material mat, bool includeClears = false)
    {
        var result = new JObject();
        foreach (var prop in _shaders[mat.shader]!)
        {
            var encoded = MaterialPropertyCodec.Encode(prop, mat, Json);
            if (encoded is not null)
                result.Add(prop.Name, encoded);
            else if (includeClears && prop.Kind == PropertyKind.Texture)
                result.Add(prop.Name, new JValue(""));
        }
        return result;
    }

    public async Task LoadMaterial(JObject data, Material mat)
    {
        foreach (var prop in _shaders[mat.shader]!)
        {
            var entry = data.Property(prop.Name);
            if (entry is null) continue;

            if (prop.Kind == PropertyKind.Texture)
            {
                var texName = entry.Value.ToObject<string>();

                if (texName is null)
                {
                    continue;
                }

                if (texName.Length is 0)
                {
                    mat.SetTexture(prop.Id, null);
                    continue;
                }

                var tex = (await _textures.ResolveAnyAsync(texName))?.Texture;
                MaterialPropertyCodec.Decode(prop, entry.Value, mat, Json, tex!);
                continue;
            }

            MaterialPropertyCodec.Decode(prop, entry.Value, mat, Json);
        }
    }

    private static JsonSerializer CreateSerializer()
    {
        var s = new JsonSerializer { ObjectCreationHandling = ObjectCreationHandling.Replace };
        s.Converters.Add(new UnityValueTypeConverter());
        return s;
    }
}