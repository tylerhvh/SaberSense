// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Data;
using SaberSense.Core.Logging;
using System;
using System.Threading.Tasks;

namespace SaberSense.Profiles;

public class SaberProfile
{
    public readonly SaberHand Hand;

    public readonly PieceRegistry<PieceDefinition> Pieces;

    public SaberScale Scale { get; set; } = SaberScale.Unit;

    public TrailSettings? Trail;

    internal ConfigSnapshot? Snapshot { get; set; }

    public bool IsBlank => Pieces.Count is 0;

    public event Action? OnChanged;

    public void NotifyChanged() => OnChanged?.Invoke();

    public SaberProfile(SaberHand hand)
    {
        Hand = hand;
        Pieces = new();
    }

    public bool TryGetSaberAsset(out SaberAsset.SaberAssetDefinition? saberAsset)
    {
        if (Pieces.TryGet(AssetTypeTag.SaberAsset, out var definition))
        {
            saberAsset = definition as SaberAsset.SaberAssetDefinition;
            return saberAsset is not null;
        }

        saberAsset = null;
        return false;
    }

    public void ApplyAssetEntry(SaberAssetEntry entry)
    {
        Pieces[entry.TypeTag] = Hand == SaberHand.Left
            ? entry.LeftPiece!
            : entry.RightPiece!;

        NotifyChanged();
    }

    public void PropagateChanges()
    {
        foreach (PieceDefinition piece in Pieces)
            piece.OwnerEntry?.SyncPiece(piece);
    }
}

internal static class SaberProfileCodec
{
    public static async Task ReadInto(SaberProfile target, JObject obj, Serializer serializer)
    {
        if (obj.TryGetValue(nameof(SaberProfile.Scale), out var scaleTkn) && scaleTkn is JObject scaleObj)
        {
            target.Scale = new()
            {
                Length = scaleObj.Value<float?>("Length") ?? 1f,
                Width = scaleObj.Value<float?>("Width") ?? 1f
            };
        }

        if (obj.TryGetValue(nameof(SaberProfile.Pieces), out var piecesToken) && piecesToken is JArray pieceArray)
        {
            foreach (var pieceToken in pieceArray)
            {
                var path = pieceToken["Path"];
                if (path is null) continue;

                var savedHash = pieceToken["ContentHash"]?.ToObject<string>();

                var composition = await serializer.ResolveSaberEntry(path.ToObject<string>()!);
                if (composition is null) continue;

                if (!string.IsNullOrEmpty(savedHash) &&
                    !string.IsNullOrEmpty(composition.LeftPiece?.Asset?.ContentHash) &&
                    !string.Equals(savedHash, composition.LeftPiece!.Asset!.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    var actualHash = composition.LeftPiece!.Asset!.ContentHash!;
                    ModLogger.ForSource("Config").Warn($"Saber at '{path}' has different content hash - skipping (expected {savedHash![..8]}..., got {actualHash[..8]}...)");
                    continue;
                }

                target.Pieces.Register(composition.TypeTag, composition[target.Hand]!);
                composition.EnsureViewed();

                var loaded = composition[target.Hand];
                if (loaded is not null)
                {
                    var def = loaded as SaberAsset.SaberAssetDefinition;
                    var snapshot = ConfigSnapshot.SeedFromDefinition(def!);
                    await snapshot.ReadFrom((JObject)pieceToken, serializer);
                    target.Snapshot = snapshot;
                }
            }
        }
    }

    public static async Task<JObject> Write(SaberProfile source, Serializer serializer)
    {
        var pieces = new JArray();
        foreach (PieceDefinition piece in source.Pieces)
        {
            if (piece?.Asset?.RelativePath == DefaultSaberProvider.DefaultSaberPath)
                continue;

            var pieceJson = (JObject)(await piece!.ToJson(serializer));

            if (source.Snapshot is not null)
                await source.Snapshot.WriteTo(pieceJson, serializer);

            pieces.Add(pieceJson);
        }

        return new JObject
        {
            [nameof(SaberProfile.Scale)] = JObject.FromObject(source.Scale),
            [nameof(SaberProfile.Pieces)] = pieces
        };
    }
}