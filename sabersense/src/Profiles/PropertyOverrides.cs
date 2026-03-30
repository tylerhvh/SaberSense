// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Data;
using System.Threading.Tasks;

namespace SaberSense.Profiles;

public abstract class PieceOverrides
{
    public TransformOverrides Transform { get; }

    protected PieceOverrides()
    {
        Transform = new();
    }

    public virtual async Task FromJson(JObject obj, IJsonProvider jsonProvider)
    {
        if (obj.TryGetValue(nameof(TransformOverrides), out var transformToken) && transformToken is JObject propObj)
            await Transform.FromJson(propObj, jsonProvider);
    }

    public virtual async Task<JToken> ToJson(IJsonProvider jsonProvider)
    {
        return new JObject
        {
            { nameof(TransformOverrides), await Transform.ToJson(jsonProvider) }
        };
    }

    public abstract void CopyFrom(PieceOverrides source);
}

public sealed class TransformOverrides
{
    private const string KeyScale = "scale";
    private const string KeyOffset = "offset";
    private const string KeyRotation = "rotation";

    public float Scale { get; set; } = 1f;
    public float Offset { get; set; }
    public float RotationDeg { get; set; }

    public Task FromJson(JObject obj, IJsonProvider jsonProvider)
    {
        Scale = obj.Value<float?>(KeyScale) ?? 1f;
        Offset = obj.Value<float?>(KeyOffset) ?? 0f;
        RotationDeg = obj.Value<float?>(KeyRotation) ?? 0f;
        return Task.CompletedTask;
    }

    public Task<JToken> ToJson(IJsonProvider jsonProvider)
    {
        var result = new JObject
        {
            [KeyScale] = Scale,
            [KeyOffset] = Offset,
            [KeyRotation] = RotationDeg
        };
        return Task.FromResult<JToken>(result);
    }
}

internal sealed class SaberAssetOverrides : PieceOverrides
{
    public override void CopyFrom(PieceOverrides source)
    {
        if (source is SaberAssetOverrides sourceCs)
        {
            Transform.Scale = sourceCs.Transform.Scale;
            Transform.RotationDeg = -sourceCs.Transform.RotationDeg;
            Transform.Offset = sourceCs.Transform.Offset;
        }
    }
}