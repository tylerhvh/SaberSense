// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

namespace SaberSense.Configuration;

internal static class ModSettingsCopier
{
    public static void CopyAll(ModSettings source, ModSettings target)
    {
        target.IsActive = source.IsActive;
        target.KeepSabersOnFocusLoss = source.KeepSabersOnFocusLoss;

        target.RandomizeSaber = source.RandomizeSaber;
        target.AnimateSelection = source.AnimateSelection;
        target.MaxGlobalWidth = source.MaxGlobalWidth;
        target.ShowGameplayButton = source.ShowGameplayButton;
        target.ShowDefaultSaber = source.ShowDefaultSaber;
        target.AudioGain = source.AudioGain;
        target.ActionKeyButton = source.ActionKeyButton;
        target.PauseKeyEnabled = source.PauseKeyEnabled;
        target.PauseKeyButton = source.PauseKeyButton;
        target.EnableEventManager = source.EnableEventManager;
        target.WarningMarkerEnabled = source.WarningMarkerEnabled;
        target.WarningTypes = new(source.WarningTypes);
        target.WarningLayerFilter = new(source.WarningLayerFilter);
        target.HidePlatform = source.HidePlatform;
        target.FloorCalibrationEnabled = source.FloorCalibrationEnabled;
        target.FloorCalibrationY = source.FloorCalibrationY;

        target.AccentColor = source.AccentColor;

        target.SmoothingEnabled = source.SmoothingEnabled;
        target.SmoothingStrength = source.SmoothingStrength;
        target.ActivePipeline = source.ActivePipeline;

        target.TransformSelections = new(source.TransformSelections);
        target.GrabSelections = new(source.GrabSelections);
        target.TrailDimensions = (source.TrailDimensions?.DeepClone() as Newtonsoft.Json.Linq.JObject)!;

        CopyTrail(source.Trail, target.Trail);
        CopyMotionBlur(source.MotionBlur, target.MotionBlur);
        CopyWorldMod(source.WorldMod, target.WorldMod);
        CopyVisibility(source.Visibility, target.Visibility);
        CopyEditor(source.Editor, target.Editor);
        CopySwingExtrapolation(source.SwingExtrapolation, target.SwingExtrapolation);
    }

    private static void CopyTrail(TrailRenderingOptions s, TrailRenderingOptions t)
    {
        t.CurveSmoothnessPercent = s.CurveSmoothnessPercent;
        t.CaptureSamplesPerSecond = s.CaptureSamplesPerSecond;
        t.VertexColorOnly = s.VertexColorOnly;
        t.OverrideTrailSortOrder = s.OverrideTrailSortOrder;
        t.LocalSpaceTrails = s.LocalSpaceTrails;
    }

    private static void CopyMotionBlur(MotionBlurConfig s, MotionBlurConfig t)
    {
        t.Enabled = s.Enabled;
        t.Strength = s.Strength;
    }

    private static void CopyWorldMod(WorldModConfig s, WorldModConfig t)
    {
        t.Enabled = s.Enabled;
        t.Modes = new(s.Modes);
        t.Strength = s.Strength;
        t.OverrideColor = s.OverrideColor;
        t.RainColor = s.RainColor;
        t.SnowColor = s.SnowColor;
        t.NetworkColor = s.NetworkColor;
    }

    private static void CopyVisibility(VisibilityConfig s, VisibilityConfig t)
    {
        t.Desktop = new(s.Desktop);
        t.Hmd = new(s.Hmd);
    }

    private static void CopyEditor(EditorConfig s, EditorConfig t)
    {
        t.PreviewSaber = s.PreviewSaber;
        t.Rotation = s.Rotation;
        t.RotationSpeed = s.RotationSpeed;
        t.Bloom = s.Bloom;
        t.DisplayTrails = s.DisplayTrails;
        t.SaberPreviewMode = s.SaberPreviewMode;
        t.SortMode = s.SortMode;
    }

    private static void CopySwingExtrapolation(SwingExtrapolationConfig s, SwingExtrapolationConfig t)
    {
        t.Enabled = s.Enabled;
        t.Strength = s.Strength;
        t.GameOnly = s.GameOnly;
    }
}