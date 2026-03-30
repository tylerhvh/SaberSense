// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Profiles;
using SaberSense.Rendering;
using SaberSense.Services;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Core;

public sealed class MenuSaberSpawner
{
    private readonly LiveSaber.Factory _saberBuilder;
    private readonly SaberLoadout _saberSet;
    private readonly ConfigManager _configManager;

    public event Action<bool>? MenuSaberToggled;

    private readonly ViewVisibilityService _viewVis;

    internal MenuSaberSpawner(LiveSaber.Factory saberBuilder, SaberLoadout saberSet, ConfigManager configManager, ViewVisibilityService viewVis)
        => (_saberBuilder, _saberSet, _configManager, _viewVis) = (saberBuilder, saberSet, configManager, viewVis);

    public async Task<LiveSaber> SpawnForMenu(Transform parent, SaberType type, Color color, bool generateTrail)
    {
        if (_configManager.CurrentTask is not null) await _configManager.CurrentTask;

        var saber = _saberBuilder.Create(ProfileFor(type));

        saber.SetColor(color);
        saber.SetParent(parent);

        if (generateTrail) saber.CreateTrail(true);

        _viewVis.ApplyVisibility(saber.GameObject, ViewFeature.Sabers, generateTrail);
        if (generateTrail) ApplyTrailVisibility(saber);

        return saber;
    }

    private void ApplyTrailVisibility(LiveSaber saber)
    {
        bool hmd = _viewVis.IsVisible(ViewFeature.Trails, ViewType.Hmd);
        bool desk = _viewVis.IsVisible(ViewFeature.Trails, ViewType.Desktop);

        if (hmd && desk)
        {
            saber.SetTrailVisibilityLayer(VisibilityLayer.Default);
            return;
        }

        if (!hmd && !desk)
        {
            saber.DestroyTrail(true);
            return;
        }

        var layer = hmd ? VisibilityLayer.HmdOnlyAndReflected : VisibilityLayer.DesktopOnlyAndReflected;
        saber.SetTrailVisibilityLayer(layer);
    }

    internal void SetMenuSaberVisible(bool isVisible)
        => MenuSaberToggled?.Invoke(isVisible);

    private SaberProfile ProfileFor(SaberType type) =>
        type is SaberType.SaberA ? _saberSet.Left : _saberSet.Right;
}