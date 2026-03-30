// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.Profiles;

namespace SaberSense.GUI.Framework.Menu.Controllers;

internal sealed class TrailSettingsController
{
    private readonly SaberLoadout _saberSet;
    private readonly PreviewSession _previewSession;
    private readonly EditScope _scope;
    private readonly IMessageBroker _broker;
    private bool _isSyncing;

    public bool IsSyncing => _isSyncing;

    public ActiveTrailState State { get; } = new();

    public TrailSettingsController(SaberLoadout saberSet, PreviewSession previewSession, EditScope scope, IMessageBroker broker)
    {
        _saberSet = saberSet;
        _previewSession = previewSession;
        _scope = scope;
        _broker = broker;
    }

    public void SetLength(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(td =>
        {
            td.TrailSettings.LengthPercent = value;
            td.Length = td.TrailSettings.TrailLength;
        });
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetWidth(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(td =>
        {
            td.TrailSettings.WidthPercent = value;
            td.Width = td.TrailSettings.TrailWidth;
        });
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetWhitestep(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(td => td.WhiteStep = value);
        _scope.Apply(s => s.SetWhiteStep(value));
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetOffset(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(td =>
        {
            td.TrailSettings.OffsetPercent = value;
            td.Offset = td.TrailSettings.PositionOffset.z;
        });
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetFlip(bool value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(td => td.Flip = value);
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetClampTexture(bool value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(td => td.ClampTexture = value);
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

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
            var td = _previewSession?.FocusedSaber?.GetTrailLayout().Primary;
            State.SyncFrom(td!);
        }
        finally { _isSyncing = false; }
    }

    public void Revert(SaberAssetEntry entry)
    {
        if (entry?.LeftPiece is not Profiles.SaberAsset.SaberAssetDefinition model) return;

        var td = _previewSession?.FocusedSaber?.GetTrailLayout().Primary;
        if (td is not null) td.RevertMaterialForSaberAsset(model);

        var tm = model.ExtractTrail(false);
        if (tm is not null)
        {
            var leftSnap = _saberSet?.Left?.Snapshot;
            if (leftSnap is not null)
                leftSnap.TrailSettings = tm.Clone();

            if (entry.RightPiece is Profiles.SaberAsset.SaberAssetDefinition rightModel)
            {
                var rtm = rightModel.ExtractTrail(false);
                var rightSnap = _saberSet?.Right?.Snapshot;
                if (rtm is not null && rightSnap is not null)
                    rightSnap.TrailSettings = rtm.Clone();
            }

            _broker?.Publish(new PreviewSaberChangedMsg(entry));
        }
    }
}