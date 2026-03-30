// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Rendering;
using SaberSense.Profiles;
using SaberSense.Profiles.SaberAsset;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.Rendering.TrailGeometry;

internal abstract class TrailDriverBase
{
    protected const int TrailRenderQueue = 3100;
    protected const int ZWriteOff = 0;
    protected const int ZTestAlways = 8;

    protected static void ApplyTrailSortOrder(Material mat)
    {
        mat.renderQueue = TrailRenderQueue;
        if (mat.HasProperty(ShaderUtils.ZWriteId)) mat.SetInt(ShaderUtils.ZWriteId, ZWriteOff);
        if (mat.HasProperty(ShaderUtils.ZTestId)) mat.SetInt(ShaderUtils.ZTestId, ZTestAlways);
    }

    public SaberTrail? TrailInstance { get; protected set; }

    protected bool CanColorMaterial;

    protected TrailDriverBase(GameObject gameObject)
    {
        TrailInstance = gameObject.AddComponent<SaberTrail>();
    }

    protected void ResolveColorability(ModSettings config, Material? trailMaterial)
    {
        CanColorMaterial = trailMaterial != null
            && !config.Trail.VertexColorOnly
            && ShaderUtils.SupportsSaberColoring(trailMaterial);
    }

    public void SetColor(Color color)
    {
        if (TrailInstance == null) return;

        TrailInstance.Color = color;
        if (CanColorMaterial)
            TrailInstance.SetMaterialBlock(ShaderUtils.ColorBlock(color));
    }

    public void SetWhiteStep(float value)
    {
        if (TrailInstance != null) TrailInstance.WhiteStep = value;
    }

    public void SetVisibilityLayer(CameraUtils.Core.VisibilityLayer layer)
    {
        if (TrailInstance != null) TrailInstance.SetLayer((int)layer);
    }

    public virtual void DestroyTrail()
    {
        TrailInstance?.TryDestroyImmediate();
        TrailInstance = null;
    }
}

internal sealed class SecondaryTrailDriver : TrailDriverBase
{
    private readonly SaberTrailMarker _marker;
    private readonly PlayerTransforms? _playerTransforms;
    private Material? _clonedMaterial;

    public SecondaryTrailDriver(GameObject gameObject, SaberTrailMarker marker, PlayerTransforms? playerTransforms)
        : base(gameObject)
    {
        _marker = marker;
        _playerTransforms = playerTransforms;
    }

    public override void DestroyTrail()
    {
        _clonedMaterial?.TryDestroyImmediate();
        _clonedMaterial = null;
        base.DestroyTrail();
    }

    public void CreateTrail(ModSettings config, bool editor)
    {
        if (_marker.PointStart == null
            || _marker.PointEnd == null
            || _marker.Length is < 1)
            return;

        var trailMat = _marker.TrailMaterial;
        if (config.Trail.OverrideTrailSortOrder && trailMat != null)
        {
            trailMat = new Material(trailMat);
            ApplyTrailSortOrder(trailMat);
            _clonedMaterial = trailMat;
        }

        var setup = new TrailSetup(
            Duration: _marker.Length,
            WhiteFade: 0f,
            Tint: Color.white,
            Resolution: config.Trail.SplineResolution,
            CaptureRate: config.Trail.CaptureSamplesPerSecond
        );

        TrailInstance!.Setup(
            setup,
            _marker.PointStart!,
            _marker.PointEnd!,
            trailMat,
            editor);
        TrailInstance.PlayerTransforms = _playerTransforms;

        ResolveColorability(config, trailMat);
    }
}

internal sealed class TrailSnapshot
{
    public TrailSettings TrailSettings { get; }
    public Transform PointStart { get; }
    public Transform PointEnd { get; }
    public MaterialHandle? Material => TrailSettings.Material;
    public bool HasMultipleTrails => AuxTrails.Count is > 0;
    public List<AuxTrailBinding> AuxTrails { get; }

    private readonly bool _reversed;

    public int Length
    {
        get => TrailSettings.TrailLength;
        set
        {
            TrailSettings.TrailLength = value;
            foreach (var aux in AuxTrails)
                aux.SyncLength(value);
        }
    }

    public float WhiteStep
    {
        get => TrailSettings.WhiteBlend;
        set => TrailSettings.WhiteBlend = value;
    }

    public float Width
    {
        get => TrailSettings.TrailWidth;
        set
        {
            TrailSettings.TrailWidth = value;
            RepositionBasePoint(value);
        }
    }

    private void RepositionBasePoint(float width)
    {
        var endZ = PointEnd.parent.localPosition.z;
        var offsetZ = TrailSettings.PositionOffset.z;
        var pos = PointStart.localPosition;
        pos.z = endZ + offsetZ - width;
        PointStart.localPosition = pos;
    }

    public float Offset
    {
        get => TrailSettings.PositionOffset.z;
        set
        {
            var offsetPos = TrailSettings.PositionOffset;
            offsetPos.z = value;
            TrailSettings.PositionOffset = offsetPos;
            PointEnd.localPosition = offsetPos;

            RepositionBasePoint(TrailSettings.TrailWidth);
        }
    }

    public bool ClampTexture
    {
        get => TrailSettings.TextureClamped;
        set => ApplyTextureWrap(value);
    }

    public bool Flip
    {
        get => TrailSettings.Reversed;
        set => TrailSettings.Reversed = value;
    }

    public void Destroy() => UnityEngine.Object.Destroy(PointEnd.gameObject);

    public static TrailSnapshot Create(
        TrailSettings settings,
        Transform pointStart,
        Transform pointEnd,
        bool isReversed,
        List<SaberTrailMarker>? secondaryMarkers = null)
    {
        var endProxy = new GameObject("TrailEnd_Proxy").transform;
        endProxy.SetParent(pointEnd, false);

        var auxBindings = secondaryMarkers?
            .Where(m => m != null)
            .Select(m => new AuxTrailBinding(m, settings.OriginalDimensions.Length))
            .ToList() ?? [];

        var snapshot = new TrailSnapshot(settings, pointStart, endProxy, isReversed, auxBindings);

        foreach (var aux in auxBindings)
            aux.SyncLength(settings.TrailLength);

        snapshot.ApplyTextureWrap(settings.TextureClamped);
        snapshot.Width = settings.TrailWidth;
        snapshot.PointEnd.localPosition = settings.PositionOffset;

        return snapshot;
    }

    private TrailSnapshot(
        TrailSettings settings,
        Transform start,
        Transform end,
        bool reversed,
        List<AuxTrailBinding> auxTrails)
    {
        TrailSettings = settings;
        PointStart = start;
        PointEnd = end;
        _reversed = reversed;
        AuxTrails = auxTrails;
    }

    private void ApplyTextureWrap(bool clamp)
    {
        TrailSettings.TextureClamped = clamp;

        if (!TrailSettings.NativeTextureWrap.HasValue) return;
        if (!TrailSettings.Material!.IsValid) return;
        if (!TrailSettings.Material.Material!.TryGetMainTexture(out var tex)) return;

        tex.wrapMode = clamp
            ? TextureWrapMode.Clamp
            : TrailSettings.NativeTextureWrap.GetValueOrDefault();
    }

    public void RevertMaterialForSaberAsset(SaberAssetDefinition saber)
    {
        TrailSettings.Material!.Revert();
    }

    public (Transform start, Transform end) GetPoints()
    {
        var (s, e) = _reversed ? (PointEnd, PointStart) : (PointStart, PointEnd);
        return Flip ? (e, s) : (s, e);
    }

    internal sealed class AuxTrailBinding
    {
        public SaberTrailMarker Trail { get; }

        private readonly int _offsetFromMain;

        public AuxTrailBinding(SaberTrailMarker trail, int baseLength)
        {
            Trail = trail;
            _offsetFromMain = baseLength - trail.Length;
        }

        public void SyncLength(int mainLength)
            => Trail.Length = Mathf.Max(0, mainLength - _offsetFromMain);
    }
}

internal interface ITrailDriver
{
    public void CreateTrail(ModSettings config, bool editor);
    public void DestroyTrail(bool immediate = false);
    public void SetTrailData(TrailSnapshot instanceTrailData);
    public void SetColor(Color color);
    public void SetWhiteStep(float value);
    public void SetVisibilityLayer(CameraUtils.Core.VisibilityLayer layer);
}

internal sealed class PrimaryTrailDriver : TrailDriverBase, ITrailDriver
{
    private TrailSnapshot? _trailSnapshot;
    private readonly global::SaberTrail? _fallbackTrail;
    private readonly PlayerTransforms? _playerTransforms;
    private Material? _fallbackMaterial;

    private int? _savedRenderQueue;
    private int? _savedZWrite;
    private int? _savedZTest;

    public PrimaryTrailDriver(GameObject gameObject, PlayerTransforms? playerTransforms)
        : base(gameObject)
    {
        _playerTransforms = playerTransforms;
    }

    public PrimaryTrailDriver(GameObject gameObject, global::SaberTrail backupTrail, PlayerTransforms? playerTransforms)
        : this(gameObject, playerTransforms)
    {
        _fallbackTrail = backupTrail;
    }

    public void CreateTrail(ModSettings config, bool editor)
    {
        if (_trailSnapshot is null)
        {
            if (_fallbackTrail == null)
            {
                return;
            }

            var trailStart = TrailInstance!.gameObject.CreateGameObject("SS_TrailTip");
            var trailEnd = TrailInstance!.gameObject.CreateGameObject("SS_TrailBase");
            trailEnd.transform.localPosition = Vector3.forward;
            var trailRenderer = _fallbackTrail.GetField<SaberTrailRenderer, global::SaberTrail>("_trailRenderer");
            _fallbackMaterial = trailRenderer.GetField<MeshRenderer, SaberTrailRenderer>("_meshRenderer").material;

            if (config.Trail.OverrideTrailSortOrder && _fallbackMaterial != null)
                ApplyTrailSortOrder(_fallbackMaterial);

            var vanillaSetup = new TrailSetup(
                Duration: 14,
                WhiteFade: 0f,
                Tint: Color.white,
                Resolution: config.Trail.SplineResolution,
                CaptureRate: 0
            );
            TrailInstance!.Setup(vanillaSetup, trailStart.transform, trailEnd.transform, _fallbackMaterial, editor);
            TrailInstance!.PlayerTransforms = _playerTransforms;
            TrailInstance!.LocalSpaceTrails = config.Trail.LocalSpaceTrails;
            return;
        }

        if (_trailSnapshot.Length is < 1)
        {
            return;
        }

        if (!_trailSnapshot!.Material!.IsValid)
        {
            ModLogger.ForSource("TrailDriver").Warn("Skipping trail creation - trail material is no longer valid.");
            return;
        }

        ApplySortOrderOverrides(_trailSnapshot.Material.Material, config.Trail.OverrideTrailSortOrder);

        var trailSetup = new TrailSetup(
            Duration: _trailSnapshot.Length,
            WhiteFade: _trailSnapshot.WhiteStep,
            Tint: Color.white,
            Resolution: config.Trail.SplineResolution,
            CaptureRate: config.Trail.CaptureSamplesPerSecond
        );
        var (pointStart, pointEnd) = _trailSnapshot.GetPoints();
        if (pointStart == null || pointEnd == null)
        {
            return;
        }

        TrailInstance!.Setup(
            trailSetup,
            pointStart,
            pointEnd,
            _trailSnapshot.Material.Material!,
            editor
        );
        TrailInstance!.PlayerTransforms = _playerTransforms;
        TrailInstance!.LocalSpaceTrails = config.Trail.LocalSpaceTrails;
        ResolveColorability(config, _trailSnapshot.Material.Material);
    }

    private void ApplySortOrderOverrides(Material? mat, bool enable)
    {
        if (mat == null) return;

        if (enable)
        {
            _savedRenderQueue ??= mat.renderQueue;
            if (mat.HasProperty(ShaderUtils.ZWriteId)) _savedZWrite ??= mat.GetInt(ShaderUtils.ZWriteId);
            if (mat.HasProperty(ShaderUtils.ZTestId)) _savedZTest ??= mat.GetInt(ShaderUtils.ZTestId);

            ApplyTrailSortOrder(mat);
        }
        else if (_savedRenderQueue.HasValue)
        {
            mat.renderQueue = _savedRenderQueue.Value;
            if (_savedZWrite.HasValue && mat.HasProperty(ShaderUtils.ZWriteId)) mat.SetInt(ShaderUtils.ZWriteId, _savedZWrite.Value);
            if (_savedZTest.HasValue && mat.HasProperty(ShaderUtils.ZTestId)) mat.SetInt(ShaderUtils.ZTestId, _savedZTest.Value);

            _savedRenderQueue = null;
            _savedZWrite = null;
            _savedZTest = null;
        }
    }

    public void DestroyTrail(bool immediate = false)
    {
        if (immediate)
            TrailInstance?.TryDestroyImmediate();
        else
            TrailInstance?.TryDestroy();

        TrailInstance = null;

        if (_fallbackMaterial != null)
        {
            _fallbackMaterial.TryDestroyImmediate();
            _fallbackMaterial = null;
        }
    }

    public override void DestroyTrail() => DestroyTrail(false);

    public void SetTrailData(TrailSnapshot instanceTrailData) => _trailSnapshot = instanceTrailData;
}