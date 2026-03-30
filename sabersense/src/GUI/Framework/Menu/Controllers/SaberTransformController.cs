// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.Profiles;
using SaberSense.Rendering;

namespace SaberSense.GUI.Framework.Menu.Controllers;

internal sealed class SaberTransformController
{
    private readonly SaberLoadout _saberSet;
    private readonly PreviewSession _previewSession;
    private readonly EditScope _scope;
    private readonly IMessageBroker _broker;
    private bool _isSyncing;

    public bool IsSyncing => _isSyncing;

    public ActiveTransformState State { get; } = new();

    public SaberTransformController(SaberLoadout saberSet, PreviewSession previewSession, EditScope scope, IMessageBroker broker)
    {
        _saberSet = saberSet;
        _previewSession = previewSession;
        _scope = scope;
        _broker = broker;
    }

    public void SetWidth(SaberAssetEntry entry, float value)
    {
        if (_isSyncing || entry is null) return;
        _scope.ApplyTransform(a => a.Width = value);
        _broker?.Publish(new SaberWidthChangedMsg(value));
    }

    public void SetLength(SaberAssetEntry entry, float value)
    {
        if (_isSyncing || entry is null) return;
        _scope.Apply(s => s.SetSaberLength(value));
    }

    public void SetRotation(SaberAssetEntry entry, float value)
    {
        if (_isSyncing || entry is null) return;
        _scope.ApplyTransform(a => a.Rotation = value);
    }

    public void SetOffset(SaberAssetEntry entry, float value)
    {
        if (_isSyncing || entry is null) return;
        _scope.ApplyTransform(a => a.Offset = value);
    }

    public void ResetWidth(SaberAssetEntry entry) => SetWidth(entry, 1f);

    public void ResetLength(SaberAssetEntry entry) => SetLength(entry, 1f);

    public void ResetRotation(SaberAssetEntry entry) => SetRotation(entry, 0f);

    public void ResetOffset(SaberAssetEntry entry) => SetOffset(entry, 0f);

    public void SyncFromActiveSaber()
    {
        _isSyncing = true;
        try
        {
            if (_saberSet?.IsEmpty == true)
            {
                State.ResetToDefaults();
                return;
            }
            if (_previewSession?.ActiveRenderer is SaberAssetRenderer csw)
            {
                if (csw.TransformBlockHandler is SaberAssetTransformHandler handler && handler.Applier is not null)
                {
                    var length = _previewSession.FocusedSaber?.Profile?.Scale.Length ?? 1f;
                    State.SyncFrom(handler.Applier.Width, length, handler.Applier.Rotation, handler.Applier.Offset);
                    return;
                }
            }
            State.ResetToDefaults();
        }
        finally { _isSyncing = false; }
    }
}