// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Configuration;

internal class WorldModConfig : BindableSettings
{
    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

    private List<int> _modes = [];
    public List<int> Modes { get => _modes; set => SetField(ref _modes, value); }

    private float _strength = 50f;
    public float Strength { get => _strength; set => SetField(ref _strength, value); }

    private bool _overrideColor;
    public bool OverrideColor { get => _overrideColor; set => SetField(ref _overrideColor, value); }

    private Color _rainColor = new(0.7f, 0.85f, 1f, 0.5f);
    public Color RainColor { get => _rainColor; set => SetField(ref _rainColor, value); }

    private Color _snowColor = new(1f, 1f, 1f, 0.65f);
    public Color SnowColor { get => _snowColor; set => SetField(ref _snowColor, value); }

    private Color _networkColor = new(0.4f, 0.8f, 1f, 0.9f);
    public Color NetworkColor { get => _networkColor; set => SetField(ref _networkColor, value); }

    public Color GetColorForMode(Core.WorldModulationMode mode) => mode switch
    {
        Core.WorldModulationMode.Rain => RainColor,
        Core.WorldModulationMode.Snow => SnowColor,
        Core.WorldModulationMode.Network => NetworkColor,
        _ => Color.white
    };

    public void SetColorForMode(Core.WorldModulationMode mode, Color c)
    {
        switch (mode)
        {
            case Core.WorldModulationMode.Rain: RainColor = c; break;
            case Core.WorldModulationMode.Snow: SnowColor = c; break;
            case Core.WorldModulationMode.Network: NetworkColor = c; break;
        }
    }
}