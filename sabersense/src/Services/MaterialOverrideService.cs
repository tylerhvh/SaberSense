// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Data;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using UnityEngine;

namespace SaberSense.Services;

internal sealed class MaterialOverrideService
{
    private readonly ShaderIntrospector _shaderCache;
    private readonly EditScope _scope;
    private readonly SaberLoadout _loadout;
    private readonly Newtonsoft.Json.JsonSerializer _json;

    public MaterialOverrideService(ShaderIntrospector shaderCache, IJsonProvider jsonProvider, EditScope scope, SaberLoadout loadout)
    {
        _shaderCache = shaderCache;
        _json = jsonProvider.Json;
        _scope = scope;
        _loadout = loadout;
    }

    public void Snapshot(string materialName, Material mat, SaberHand? sourceHand = null)
    {
        if (_shaderCache is null || mat?.shader == null) return;

        bool overrideOff = mat.HasProperty(ShaderUtils.CustomColorToggleId) && mat.GetFloat(ShaderUtils.CustomColorToggleId) > 0.5f;

        var focusedHand = sourceHand ?? _scope.FocusedHand;
        var focusedSnapshot = _loadout[focusedHand].Snapshot;
        var otherSnapshot = _loadout[focusedHand.Other()].Snapshot;

        if (focusedSnapshot is null) return;

        JObject overrideObj;
        if (focusedSnapshot.MaterialOverrides.TryGetValue(materialName, out var existing))
            overrideObj = existing;
        else
            overrideObj = new JObject();

        foreach (var prop in _shaderCache[mat.shader]!)
        {
            if (overrideOff && prop.Name == "_Color") continue;
            var json = MaterialPropertyCodec.Encode(prop, mat, _json);
            if (json is null)
            {
                if (prop.Kind == PropertyKind.Texture)
                {
                    bool isSplitTex = focusedSnapshot.IsPropertySplit(materialName, prop.Name);
                    if (isSplitTex)
                    {
                        if (overrideObj[prop.Name] is JObject existingProp && existingProp.ContainsKey("Left"))
                            existingProp["Left"] = new JValue("");
                    }
                    else
                    {
                        overrideObj[prop.Name] = new JValue("");
                    }
                }
                continue;
            }

            bool isSplit = focusedSnapshot.IsPropertySplit(materialName, prop.Name);
            if (isSplit)
            {
                if (overrideObj[prop.Name] is JObject existingProp && existingProp.ContainsKey("Left"))
                    existingProp["Left"] = json;
            }
            else
            {
                overrideObj[prop.Name] = json;
            }
        }

        focusedSnapshot.MaterialOverrides[materialName] = overrideObj;

        if (_scope.Linked && otherSnapshot is not null && otherSnapshot != focusedSnapshot)
            otherSnapshot.MaterialOverrides[materialName] = (JObject)overrideObj.DeepClone();
    }

    public void SnapshotSplit(string materialName, string propName,
        JToken value, SaberHand hand)
    {
        var focusedHand = _scope.FocusedHand;
        var focusedSnapshot = _loadout[focusedHand].Snapshot;
        var otherSnapshot = _loadout[focusedHand.Other()].Snapshot;
        if (focusedSnapshot is null) return;

        focusedSnapshot.SetPropertyForHand(materialName, propName, value, hand);

        if (_scope.Linked && otherSnapshot is not null && otherSnapshot != focusedSnapshot)
            otherSnapshot.SetPropertyForHand(materialName, propName, value, hand);
    }
}