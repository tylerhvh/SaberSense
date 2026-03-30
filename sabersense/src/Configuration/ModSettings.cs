// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Core.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Configuration;

internal sealed class ModSettings : BindableSettings
{
    private bool _isActive = true;
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }

    private bool _randomizeSaber;
    public bool RandomizeSaber { get => _randomizeSaber; set => SetField(ref _randomizeSaber, value); }

    private bool _animateSelection = true;
    public bool AnimateSelection { get => _animateSelection; set => SetField(ref _animateSelection, value); }

    private float _maxGlobalWidth = 3f;
    public float MaxGlobalWidth { get => _maxGlobalWidth; set => SetField(ref _maxGlobalWidth, value); }

    private bool _showGameplayButton = true;
    public bool ShowGameplayButton { get => _showGameplayButton; set => SetField(ref _showGameplayButton, value); }

    private bool _showDefaultSaber;
    public bool ShowDefaultSaber { get => _showDefaultSaber; set => SetField(ref _showDefaultSaber, value); }

    private float _audioGain = 1f;
    public float AudioGain { get => _audioGain; set => SetField(ref _audioGain, value); }

    private int _actionKeyButton;
    public int ActionKeyButton { get => _actionKeyButton; set => SetField(ref _actionKeyButton, value); }

    private bool _pauseKeyEnabled;
    public bool PauseKeyEnabled { get => _pauseKeyEnabled; set => SetField(ref _pauseKeyEnabled, value); }

    private int _pauseKeyButton;
    public int PauseKeyButton { get => _pauseKeyButton; set => SetField(ref _pauseKeyButton, value); }

    private bool _enableEventManager = true;
    public bool EnableEventManager { get => _enableEventManager; set => SetField(ref _enableEventManager, value); }

    private bool _warningMarkerEnabled;
    public bool WarningMarkerEnabled { get => _warningMarkerEnabled; set => SetField(ref _warningMarkerEnabled, value); }

    private List<int> _warningTypes = [0];
    public List<int> WarningTypes { get => _warningTypes; set => SetField(ref _warningTypes, value); }

    private List<int> _warningLayerFilter = [0, 1, 2];
    public List<int> WarningLayerFilter { get => _warningLayerFilter; set => SetField(ref _warningLayerFilter, value); }

    private bool _hidePlatform;
    public bool HidePlatform { get => _hidePlatform; set => SetField(ref _hidePlatform, value); }

    private bool _keepSabersOnFocusLoss;
    public bool KeepSabersOnFocusLoss { get => _keepSabersOnFocusLoss; set => SetField(ref _keepSabersOnFocusLoss, value); }

    private bool _floorCalibrationEnabled;
    public bool FloorCalibrationEnabled { get => _floorCalibrationEnabled; set => SetField(ref _floorCalibrationEnabled, value); }

    private float _floorCalibrationY;
    public float FloorCalibrationY { get => _floorCalibrationY; set => SetField(ref _floorCalibrationY, value); }

    private Color _accentColor = new(0.62f, 0.79f, 0.16f, 1f);
    public Color AccentColor { get => _accentColor; set => SetField(ref _accentColor, value); }

    private bool _smoothingEnabled;
    public bool SmoothingEnabled { get => _smoothingEnabled; set => SetField(ref _smoothingEnabled, value); }

    private float _smoothingStrength;
    public float SmoothingStrength { get => _smoothingStrength; set => SetField(ref _smoothingStrength, value); }

    private ESaberPipeline _activePipeline;
    public ESaberPipeline ActivePipeline { get => _activePipeline; set => SetField(ref _activePipeline, value); }

    private List<int> _transformSelections = [];
    public List<int> TransformSelections { get => _transformSelections; set => SetField(ref _transformSelections, value); }

    private List<int> _grabSelections = [];
    public List<int> GrabSelections { get => _grabSelections; set => SetField(ref _grabSelections, value); }

    private JObject _trailDimensions = [];
    [JsonProperty]
    public JObject TrailDimensions { get => _trailDimensions; set => SetField(ref _trailDimensions, value); }

    private TrailRenderingOptions _trail = new();
    public TrailRenderingOptions Trail { get => _trail; set => SetField(ref _trail, value); }

    private MotionBlurConfig _motionBlur = new();
    public MotionBlurConfig MotionBlur { get => _motionBlur; set => SetField(ref _motionBlur, value); }

    private WorldModConfig _worldMod = new();
    public WorldModConfig WorldMod { get => _worldMod; set => SetField(ref _worldMod, value); }

    private VisibilityConfig _visibility = new();
    public VisibilityConfig Visibility { get => _visibility; set => SetField(ref _visibility, value); }

    private EditorConfig _editor = new();
    public EditorConfig Editor { get => _editor; set => SetField(ref _editor, value); }

    private SwingExtrapolationConfig _swingExtrapolation = new();
    public SwingExtrapolationConfig SwingExtrapolation { get => _swingExtrapolation; set => SetField(ref _swingExtrapolation, value); }

    public (int Length, float Width)? GetTrailDimensions(string saberName)
    {
        try
        {
            if (TrailDimensions is null || !TrailDimensions.ContainsKey(saberName)) return null;
            if (TrailDimensions[saberName] is not JObject entry) return null;
            return (entry.Value<int>("Length"), entry.Value<float>("Width"));
        }
        catch (System.Exception ex)
        {
            ModLogger.ForSource("ModSettings").Warn($"Failed to read trail dimensions for '{saberName}': {ex.Message}");
            return null;
        }
    }

    public void SetTrailDimensions(string saberName, int length, float width)
    {
        TrailDimensions ??= [];
        TrailDimensions[saberName] = new JObject { ["Length"] = length, ["Width"] = width };
        Notify(nameof(TrailDimensions));
    }

    internal void ResetToDefaults()
    {
        IsActive = true;

        RandomizeSaber = false;
        AnimateSelection = true;
        MaxGlobalWidth = 3f;
        ShowGameplayButton = true;
        ShowDefaultSaber = false;
        AudioGain = 1f;
        ActionKeyButton = 0;
        PauseKeyEnabled = false;
        PauseKeyButton = 0;
        EnableEventManager = true;
        WarningMarkerEnabled = false;
        WarningTypes = [0];
        WarningLayerFilter = [0, 1, 2];
        HidePlatform = false;
        KeepSabersOnFocusLoss = false;
        FloorCalibrationEnabled = false;
        FloorCalibrationY = 0f;

        AccentColor = new Color(0.62f, 0.79f, 0.16f, 1f);

        SmoothingEnabled = false;
        SmoothingStrength = 0f;
        ActivePipeline = default;

        TransformSelections = [];
        GrabSelections = [];
        TrailDimensions = [];

        Trail = new();
        MotionBlur = new();
        WorldMod = new();
        Visibility = new();
        Editor = new();
        SwingExtrapolation = new();
    }
}