// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Data;
using SaberSense.Core.Utilities;
using SaberSense.Profiles.SaberAsset;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaberSense.Profiles;

internal sealed class ConfigSnapshot
{
    public Dictionary<string, JObject> MaterialOverrides { get; set; } = [];

    public TrailSettings? TrailSettings { get; set; }

    public JObject? ModifierState { get; set; }

    public TransformOverrides Transform { get; set; } = new();

    private SplitPropertyManager? _splitPropertyManager;
    private SplitPropertyManager SplitProperties => _splitPropertyManager ??= new(MaterialOverrides);

    public bool IsPropertySplit(string matName, string propName) => SplitProperties.IsPropertySplit(matName, propName);
    public void SplitProperty(string matName, string propName) => SplitProperties.SplitProperty(matName, propName);
    public void UnsplitProperty(string matName, string propName) => SplitProperties.UnsplitProperty(matName, propName);
    public JToken? GetPropertyForHand(string matName, string propName, SaberHand hand)
        => SplitProperties.GetPropertyForHand(matName, propName, hand);
    public void SetPropertyForHand(string matName, string propName, JToken value, SaberHand hand)
        => SplitProperties.SetPropertyForHand(matName, propName, value, hand);

    public static ConfigSnapshot CreateDefault() => new();

    public static ConfigSnapshot SeedFromDefinition(SaberAsset.SaberAssetDefinition def)
    {
        var snapshot = new ConfigSnapshot();
        if (def?.TrailSettings is not null)
        {
            snapshot.TrailSettings = def.TrailSettings.Clone();
            def.ComputeSaberExtent(snapshot.TrailSettings);
            var dims = def.GetCachedDimensions();
            if (dims is not null)
            {
                snapshot.TrailSettings.TrailLength = dims.Value.Length;
                snapshot.TrailSettings.TrailWidth = dims.Value.Width;
            }
        }
        return snapshot;
    }

    public ConfigSnapshot Clone()
    {
        var clone = new ConfigSnapshot();
        foreach (var kv in MaterialOverrides)
            clone.MaterialOverrides[kv.Key] = (JObject)kv.Value.DeepClone();
        clone.TrailSettings = TrailSettings?.Clone();
        clone.ModifierState = (JObject?)ModifierState?.DeepClone();
        clone.Transform = new()
        {
            Scale = Transform.Scale,
            Offset = Transform.Offset,
            RotationDeg = Transform.RotationDeg
        };
        return clone;
    }

    public async Task ReadFrom(JObject obj, Serializer serializer)
    {
        var trailToken = obj[nameof(TrailSettings)];
        if (trailToken is JObject trailObj)
        {
            TrailSettings ??= new();
            await TrailSettingsCodec.ReadInto(TrailSettings, trailObj, serializer);
        }

        MaterialOverrides.Clear();
        _splitPropertyManager = null;
        if (obj.TryGetObject(nameof(MaterialOverrides), out var moObj))
        {
            foreach (var prop in moObj.Properties())
                MaterialOverrides[prop.Name] = (JObject)prop.Value;
        }

        if (obj.TryGetObject("ComponentModifiers", out var modObj))
        {
            ModifierState = modObj;
        }

        if (obj.TryGetObject("Properties", out var pObj))
        {
            if (pObj.TryGetObject(nameof(TransformOverrides), out var tObj))
                await Transform.FromJson(tObj, serializer);
        }
    }

    public async Task WriteTo(JObject obj, Serializer serializer)
    {
        if (TrailSettings is not null)
        {
            obj[nameof(TrailSettings)] = await TrailSettingsCodec.Write(TrailSettings, serializer);
        }

        if (MaterialOverrides.Count is > 0)
        {
            var moObj = new JObject();
            foreach (var kv in MaterialOverrides)
                moObj.Add(kv.Key, kv.Value);
            obj[nameof(MaterialOverrides)] = moObj;
        }

        if (ModifierState is not null)
        {
            obj["ComponentModifiers"] = ModifierState;
        }

        obj["Properties"] = new JObject
        {
            { nameof(TransformOverrides), await Transform.ToJson(serializer) }
        };
    }

    public async Task ApplyModifierState(SaberSense.Behaviors.ComponentModifierRegistry registry, IJsonProvider jsonProvider)
    {
        if (ModifierState is not null && registry is not null)
        {
            await registry.FromJson(ModifierState, jsonProvider);
        }
    }

    public async Task CaptureModifierState(SaberSense.Behaviors.ComponentModifierRegistry registry, IJsonProvider jsonProvider)
    {
        if (registry is not null)
        {
            ModifierState = (JObject)await registry.ToJson(jsonProvider);
        }
    }
}