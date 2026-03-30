// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Data;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Rendering;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Profiles;

public readonly record struct TrailDimensions(int Length, float Width);

public sealed class TrailSettings
{
    public TrailDimensions OriginalDimensions { get; private set; }

    public Vector3 PositionOffset { get; set; }
    public int TrailLength { get; set; }
    public float TrailWidth { get; set; }

    public float SaberExtent { get; set; } = 1f;

    public float MaxTrailWidth { get; set; } = 1f;

    public float TrailEndZ { get; set; } = 1f;
    public float WhiteBlend { get; set; }
    public bool TextureClamped { get; set; }
    public bool Reversed { get; set; }
    public string? OriginAssetPath { get; set; }

    [JsonIgnore] public MaterialHandle? Material { get; set; }
    [JsonIgnore] public List<SaberTrailMarker>? OriginTrails { get; set; }

    [JsonIgnore] public JObject? DeferredMaterialJson { get; set; }
    public TextureWrapMode? NativeTextureWrap { get; set; }

    public TrailSettings(
        MaterialHandle material,
        int trailLength,
        float trailWidth,
        Vector3 positionOffset = default,
        float whiteBlend = 0f,
        TextureWrapMode? nativeWrap = null,
        string originPath = "")
    {
        Material = material;
        TrailLength = trailLength;
        TrailWidth = trailWidth;
        OriginalDimensions = new(trailLength, trailWidth);
        PositionOffset = positionOffset;
        WhiteBlend = whiteBlend;
        NativeTextureWrap = nativeWrap;
        OriginAssetPath = originPath;
        SaberExtent = 1f;
    }

    public TrailSettings() { }

    public float WidthPercent
    {
        get => MaxTrailWidth > 0f ? (TrailWidth / MaxTrailWidth) * 100f : 0f;
        set => TrailWidth = MaxTrailWidth > 0f ? (value / 100f) * MaxTrailWidth : 0f;
    }

    private const int MaxTrailFrames = 40;

    public float LengthPercent
    {
        get => (TrailLength / (float)MaxTrailFrames) * 100f;
        set => TrailLength = Mathf.Clamp(Mathf.RoundToInt((value / 100f) * MaxTrailFrames), 0, MaxTrailFrames);
    }

    public float OffsetPercent
    {
        get => SaberExtent > 0f ? (PositionOffset.z / SaberExtent) * 100f : 0f;
        set
        {
            var pos = PositionOffset;
            pos.z = SaberExtent > 0f ? (value / 100f) * SaberExtent : 0f;
            PositionOffset = pos;
        }
    }

    public TrailSettings CloneFrom(TrailSettings source)
    {
        CopyUserSettings(source);

        SaberExtent = source.SaberExtent;
        MaxTrailWidth = source.MaxTrailWidth;
        TrailEndZ = source.TrailEndZ;

        return this;
    }

    public TrailSettings CloneUserSettings(TrailSettings source)
    {
        CopyUserSettings(source);
        return this;
    }

    private void CopyUserSettings(TrailSettings source)
    {
        PositionOffset = source.PositionOffset;
        TrailWidth = source.TrailWidth;
        TrailLength = source.TrailLength;
        OriginalDimensions = source.OriginalDimensions;
        WhiteBlend = source.WhiteBlend;
        OriginAssetPath = source.OriginAssetPath;
        TextureClamped = source.TextureClamped;
        Reversed = source.Reversed;
        NativeTextureWrap = source.NativeTextureWrap;

        Material ??= new(null);
        if (source.Material?.Material != null)
        {
            var newMat = new Material(source.Material.Material);
            if (Material.IsOwned)
                Material.Material?.TryDestroyImmediate();
            Material.Material = newMat;
        }
    }

    public TrailSettings Clone() => new TrailSettings().CloneFrom(this);
}

internal static class TrailSettingsCodec
{
    public static async Task ReadInto(TrailSettings target, JObject obj, Serializer serializer)
    {
        target.TrailLength = obj.Value<int?>(nameof(target.TrailLength)) ?? target.TrailLength;
        target.TrailWidth = obj.Value<float?>(nameof(target.TrailWidth)) ?? target.TrailWidth;
        target.WhiteBlend = obj.Value<float?>(nameof(target.WhiteBlend)) ?? target.WhiteBlend;
        target.TextureClamped = obj.Value<bool?>(nameof(target.TextureClamped)) ?? target.TextureClamped;
        target.Reversed = obj.Value<bool?>(nameof(target.Reversed)) ?? target.Reversed;
        target.OriginAssetPath = obj.Value<string>(nameof(target.OriginAssetPath)) ?? "";
        target.SaberExtent = obj.Value<float?>(nameof(target.SaberExtent)) ?? target.SaberExtent;
        target.MaxTrailWidth = obj.Value<float?>(nameof(target.MaxTrailWidth)) ?? target.MaxTrailWidth;
        target.TrailEndZ = obj.Value<float?>(nameof(target.TrailEndZ)) ?? target.TrailEndZ;

        if (obj.TryGetValue(nameof(target.PositionOffset), out var posToken))
            target.PositionOffset = posToken.ToObject<Vector3>(serializer.Json);

        if (obj.TryGetValue(nameof(target.NativeTextureWrap), out var wrapToken))
            target.NativeTextureWrap = wrapToken.ToObject<TextureWrapMode?>(serializer.Json);

        if (!string.IsNullOrEmpty(target.OriginAssetPath))
            await ResolveOriginAsset(target, serializer, target.OriginAssetPath);

        if (obj.TryGetValue("Material", out var materialToken) && materialToken is JObject matObj)
        {
            target.Material ??= new(null);
            var liveMat = target.Material.Material;
            ModLogger.ForSource("TrailCodec").Info($"ReadInto: Material JSON has {matObj.Count} props, liveMat={(liveMat != null)} id={liveMat?.GetInstanceID()}");
            if (liveMat != null)
                await serializer.LoadMaterial(matObj, liveMat);
            else
                target.DeferredMaterialJson = matObj;
        }
        else
        {
            ModLogger.ForSource("TrailCodec").Info($"ReadInto: No 'Material' key in JSON");
        }
    }

    public static Task<JToken> Write(TrailSettings source, Serializer serializer)
    {
        var obj = JObject.FromObject(source, serializer.Json);

        obj["OriginalLength"] = source.OriginalDimensions.Length;
        obj["OriginalWidth"] = source.OriginalDimensions.Width;

        obj.Remove("BaseLength");

        if (source.Material is { IsValid: true })
        {
            var matJson = serializer.SerializeMaterial(source.Material.Material!, includeClears: true);
            ModLogger.ForSource("TrailCodec").Info($"Write: Serialized material: valid=True id={source.Material!.Material!.GetInstanceID()} props={((JObject)matJson).Count}");
            obj.Add("Material", matJson);
        }
        else
        {
            ModLogger.ForSource("TrailCodec").Info($"Write: Material NOT serialized: mat={(source.Material is not null)} valid={(source.Material?.IsValid)}");
        }
        return Task.FromResult<JToken>(obj);
    }

    private static async Task ResolveOriginAsset(TrailSettings target, Serializer serializer, string originPath)
    {
        var entry = await serializer.ResolveSaberEntry(originPath);
        if (entry?.LeftPiece is not SaberAsset.SaberAssetDefinition cs) return;

        var originTrail = cs.ExtractTrail(true);
        if (originTrail is null) return;

        target.Material = new(new UnityEngine.Material(originTrail.Material!.Material!));
        target.OriginTrails = SaberComponentDiscovery.GetTrails(cs.Prefab);
    }
}