// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;

namespace SaberSense.Profiles;

public sealed class SaberAssetEntry : IDisposable, ISaberListEntry
{
    public readonly AssetTypeTag TypeTag;

    public readonly AuxObjectManager AuxObjects;

    private readonly PieceDefinition? _left;
    private readonly PieceDefinition? _right;
    private SaberDisplayInfo _displayInfo;
    private bool _hasBeenViewed;

    public string DisplayName => _displayInfo.Name ?? string.Empty;
    public string CreatorName => _displayInfo.Author ?? string.Empty;
    public Sprite? CoverImage => _displayInfo.Cover;
    public bool IsPinned => _displayInfo.IsPinned;
    public bool IsSPICompatible { get; set; } = true;

    public bool IsAssetStale => AuxObjects.IsStale;

    public PieceDefinition? LeftPiece => _left;

    public PieceDefinition? RightPiece => _right ?? _left;

    public PieceDefinition? this[SaberHand hand] => hand == SaberHand.Left ? LeftPiece : RightPiece;

    private SaberAssetEntry(
        AssetTypeTag typeTag,
        PieceDefinition? left,
        PieceDefinition? right,
        GameObject auxPrefab)
    {
        TypeTag = typeTag;
        _left = left;
        _right = right;

        AuxObjects = new(auxPrefab, right?.Asset.Prefab);

        if (_left is not null)
        {
            _left.OwnerEntry = this;
            _left.AuxObjects = AuxObjects;
            _left.OnCreated();
            _displayInfo = _left.GetDisplayInfo();
        }

        if (_right is not null)
        {
            _right.OwnerEntry = this;
            _right.AuxObjects = AuxObjects;
            _right.OnCreated();
        }
    }

    public static SaberAssetEntry Create(
        AssetTypeTag typeTag,
        PieceDefinition? left,
        PieceDefinition? right,
        GameObject auxPrefab) => new(typeTag, left, right, auxPrefab);

    public void Dispose()
    {
        AuxObjects?.Destroy();

        try
        {
            if (_left is not null)
            {
                _left.Asset.Unload();
                _left.Dispose();
            }
        }
        finally
        {
            if (_right is not null)
            {
                _right.Asset.Unload();
                _right.Dispose();
            }
        }
    }

    public void EnsureViewed()
    {
        if (_hasBeenViewed || _left is null) return;
        _hasBeenViewed = true;
        _left.OnFirstViewed();
    }

    public void PersistAuxData()
    {
        _left?.PersistAuxData();
        if (_right is not null && _right != _left)
            _right.PersistAuxData();
    }

    public void SyncPiece(PieceDefinition editedPiece)
    {
        if (editedPiece != _left && editedPiece != _right)
            throw new System.ArgumentException("editedPiece must be either the Left or Right piece of this entry.", nameof(editedPiece));

        var counterpart = _left == editedPiece ? _right : _left;
        counterpart?.CloneStateFrom(editedPiece);
    }

    public void DestroyAuxObjects() => AuxObjects.Destroy();

    public void SetPinned(bool state) => _displayInfo = new(
        _displayInfo.Name!, _displayInfo.Author!, _displayInfo.Cover, state);
}