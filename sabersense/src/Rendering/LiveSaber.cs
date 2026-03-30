// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Catalog.Data;
using SaberSense.Configuration;
using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using SaberSense.Profiles.SaberAsset;
using SaberSense.Rendering.TrailGeometry;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace SaberSense.Rendering;

internal readonly record struct TrailLayout(
    TrailSnapshot? Primary,
    IReadOnlyList<SaberTrailMarker> AuxMarkers)
{
    internal static readonly TrailLayout None = new(null, Array.Empty<SaberTrailMarker>());
}

public class LiveSaber
{
    public const string DisplayName = "SS Saber";

    internal ITrailDriver? TrailHandler { get; set; }
    internal SaberSense.Gameplay.SaberEventDispatcher? EventDispatcher { get; private set; }
    internal readonly SaberProfile Profile;
    internal readonly PieceRegistry<PieceRenderer> Pieces;

    public readonly Transform CachedTransform;
    public readonly GameObject GameObject;
    public PlayerTransforms? PlayerTransforms { get; internal set; }

    private readonly ModSettings _trailConfig;
    private readonly PlayerDataModel _playerDataModel;
    private readonly LiveSaberRegistry _activeInstances;
    private readonly ShaderRegistry _shaders;

    private TrailController? _trailController;
    private SaberMotionBlur? _motionBlur;

    internal event Action? OnDestroyed;

    [Inject]
    internal LiveSaber(
        SaberProfile profile,
        PieceRenderer.Factory rendererFactory,
        ModSettings trailConfig,
        List<ISaberFinalizer> postProcessors,
        PlayerDataModel playerDataModel,
        LiveSaberRegistry saberInstanceList,
        ShaderRegistry shaders,
        IJsonProvider jsonProvider)
    {
        _trailConfig = trailConfig;
        _playerDataModel = playerDataModel;
        _activeInstances = saberInstanceList;
        _shaders = shaders;

        Profile = profile;
        GameObject = new GameObject(DisplayName);
        DestroySentinel.Attach(GameObject, OnGameObjectDestroyed);

        CachedTransform = GameObject.transform;
        Pieces = new();

        ApplyColorSchemeGlobals();
        ApplyScale();
        AssemblePieces(rendererFactory, jsonProvider);

        foreach (var pp in postProcessors) pp.ProcessSaber(this);

        InjectEventsFromPieces();

        _activeInstances.Register(this);
    }

    public void SetParent(Transform parent) => CachedTransform.SetParent(parent, false);

    public void SetColor(Color color)
    {
        foreach (var piece in Pieces) piece.ApplyColor(color);

        TrailHandler?.SetColor(color);
        _trailController?.TintSecondaryTrails(color);
        _motionBlur?.RefreshColors();
    }

    internal void SwapToHand(SaberHand hand, SaberSense.Services.SharedMaterialPool pool)
    {
        var resolver = new SaberSense.Core.Utilities.MaterialNameResolver();
        var renderers = new List<Renderer>();
        GameObject.GetComponentsInChildren(true, renderers);

        var swaps = new List<(Material old, Material @new)>();

        var unswapped = new HashSet<Material>();

        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string baseName = resolver.Resolve(mats[i]);
                var poolMat = pool.Get(baseName, hand);
                if (poolMat != null && poolMat != mats[i])
                {
                    swaps.Add((mats[i], poolMat));
                    mats[i] = poolMat;
                    changed = true;
                }
                else if (poolMat == null)
                {
                    unswapped.Add(mats[i]);
                }
            }
            if (changed) rend.sharedMaterials = mats;
        }

        foreach (var piece in Pieces)
        {
            foreach (var (old, @new) in swaps)
                piece.SwapTintMaterial(old, @new);

            if (unswapped.Count is > 0)
                piece.ClearUnswappedTintMaterials(unswapped);
        }
    }

    public void SetWhiteStep(float value) => TrailHandler?.SetWhiteStep(value);

    public void SetTrailVisibilityLayer(CameraUtils.Core.VisibilityLayer layer)
    {
        TrailHandler?.SetVisibilityLayer(layer);
        _trailController?.SetVisibilityLayer(layer);
    }

    public void SetMotionBlurVisibilityLayer(CameraUtils.Core.VisibilityLayer layer)
    {
        _motionBlur?.SetLayer((int)layer);
    }

    public void RefreshMotionBlurColors()
    {
        _motionBlur?.RefreshColors();
    }

    public void CreateTrail(bool editorMode, global::SaberTrail? fallbackTrail = null)
    {
        _trailController = new(this, _trailConfig);
        _trailController!.Activate(editorMode, fallbackTrail!);
        TrailHandler = _trailController.PrimaryHandler;
    }

    public void CreateMotionBlur(float strength)
    {
        if (_motionBlur != null) return;

        _motionBlur = GameObject.AddComponent<SaberMotionBlur>();
        _motionBlur.Strength = strength;

        (float, float)? bounds = null;
        if (TryGetSaberAssetRenderer(out var sar) && sar!.Definition is SaberAssetDefinition def)
            bounds = def.Asset?.ParsedBounds;

        _motionBlur.Init(GameObject, bounds, _shaders.InternalColored);
    }

    public void DestroyTrail(bool immediate = false)
    {
        _trailController?.Teardown(immediate);
        _trailController = null;
        TrailHandler = null;
    }

    public void Destroy()
    {
        _activeInstances.Unregister(this);

        OnDestroyed?.Invoke();
        GameObject?.TryDestroy();
    }

    public void SetSaberWidth(float width)
    {
        Profile.Scale.Width = width;
        ApplyScale();
    }

    public void SetSaberLength(float length)
    {
        Profile.Scale.Length = length;
        ApplyScale();
    }

    private void ApplyScale()
    {
        var s = Profile.Scale;
        if (GameObject != null)
            GameObject.transform.localScale = new Vector3(s.Width, s.Width, s.Length);
        _motionBlur?.NotifyTransformChanged();
    }

    internal static void WithTransformApplier(LiveSaber? saber, Action<TransformApplier> action)
    {
        if (saber is null) return;
        if (!saber.TryGetSaberAssetRenderer(out var renderer)) return;
        if (renderer!.TransformBlockHandler is SaberAssetTransformHandler handler && handler.Applier is not null)
            action(handler.Applier);
    }

    internal static void WithTrailData(LiveSaber? saber, Action<TrailSnapshot> action)
    {
        if (saber is null) return;
        var td = saber.GetTrailLayout().Primary;
        if (td is not null) action(td);
    }

    internal bool TryGetSaberAssetRenderer(out SaberAssetRenderer? renderer)
    {
        if (Pieces.TryGet(AssetTypeTag.SaberAsset, out var piece) && piece is SaberAssetRenderer csr)
        {
            renderer = csr;
            return true;
        }
        renderer = null;
        return false;
    }

    internal TrailLayout GetTrailLayout()
    {
        if (!TryGetSaberAssetRenderer(out var renderer) || renderer!.TrailData is not { } data)
            return TrailLayout.None;

        var aux = data.AuxTrails is { Count: > 0 }
            ? (IReadOnlyList<SaberTrailMarker>)data.AuxTrails.Select(t => t.Trail).ToList()
            : Array.Empty<SaberTrailMarker>();

        return new TrailLayout(data, aux);
    }

    internal void ActivateForGameplay(Transform parent, global::SaberTrail? fallbackTrail)
    {
        SetParent(parent);
        CreateTrail(editorMode: false, fallbackTrail);
        GameObject.SetLayerRecursively(VisibilityLayer.Saber);
    }

    private void ApplyColorSchemeGlobals()
    {
        if (_playerDataModel?.playerData?.colorSchemesSettings?.GetSelectedColorScheme() is { } scheme)
        {
            Shader.SetGlobalColor(ShaderUtils.LeftHandColorId, scheme.saberAColor);
            Shader.SetGlobalColor(ShaderUtils.RightHandColorId, scheme.saberBColor);
        }
    }

    private GameObject? _eventTargetRoot;

    private void InjectEventsFromPieces()
    {
        if (!TryGetSaberAssetRenderer(out var sar)) return;
        var parseResult = sar!.Definition?.Asset?.ParseResult;
        if (parseResult?.HasEvents != true) return;

        var prefabRoot = sar.Definition?.Asset?.Prefab?.transform.parent?.gameObject;

        if (prefabRoot != null)
        {
            _eventTargetRoot = new GameObject("SS_EventTargets");
            _eventTargetRoot.transform.SetPositionAndRotation(
                prefabRoot.transform.position, prefabRoot.transform.rotation);
            _eventTargetRoot.transform.localScale = prefabRoot.transform.localScale;

            for (int i = 0; i < prefabRoot.transform.childCount; i++)
            {
                var child = prefabRoot.transform.GetChild(i);
                if (child.name is "LeftSaber" or "RightSaber") continue;
                var clone = UnityEngine.Object.Instantiate(child.gameObject, _eventTargetRoot.transform, false);
                clone.name = child.name;
            }
        }

        EventDispatcher = Core.Utilities.PrefabComponentInjector.InjectEvents(
            GameObject, parseResult, _eventTargetRoot);
    }

    private void AssemblePieces(PieceRenderer.Factory rendererFactory, IJsonProvider jsonProvider)
    {
        if (Profile.Pieces.TryGet(AssetTypeTag.SaberAsset, out var wholeDef))
        {
            SpawnPiece(AssetTypeTag.SaberAsset, wholeDef, rendererFactory, jsonProvider);
            return;
        }

        ReadOnlySpan<PartCategory> partOrder = [
            PartCategory.Pommel, PartCategory.Handle, PartCategory.Emitter, PartCategory.Blade
        ];

        foreach (var part in partOrder)
        {
            var tag = new AssetTypeTag(AssetKind.Model, part);
            if (Profile.Pieces.TryGet(tag, out var def))
                SpawnPiece(tag, def, rendererFactory, jsonProvider);
        }
    }

    private void SpawnPiece(AssetTypeTag tag, PieceDefinition definition,
        PieceRenderer.Factory rendererFactory, IJsonProvider jsonProvider)
    {
        var renderer = rendererFactory.Create(definition);

        if (renderer is SaberAssetRenderer sar)
            sar.Snapshot = Profile.Snapshot;

        renderer.Initialize();
        renderer.AttachTo(CachedTransform);
        Pieces.Register(tag, renderer);

        var snapshot = Profile.Snapshot;
        if (snapshot?.ModifierState is not null && definition.ComponentModifiers is not null)
            _ = snapshot.ApplyModifierState(definition.ComponentModifiers, jsonProvider);
    }

    private void OnGameObjectDestroyed()
    {
        DestroyTrail();
        if (_motionBlur != null)
        {
            UnityEngine.Object.Destroy(_motionBlur);
            _motionBlur = null;
        }
        if (_eventTargetRoot != null)
        {
            UnityEngine.Object.Destroy(_eventTargetRoot);
            _eventTargetRoot = null;
        }
        foreach (var piece in Pieces) piece.Dispose();
    }

    internal sealed class Factory : PlaceholderFactory<SaberProfile, LiveSaber> { }
}

internal sealed class DestroySentinel : MonoBehaviour
{
    Action? _onDestroy;

    internal static DestroySentinel Attach(GameObject target, Action onDestroy)
    {
        var sentinel = target.AddComponent<DestroySentinel>();
        sentinel._onDestroy = onDestroy;
        return sentinel;
    }

    void OnDestroy() => _onDestroy?.Invoke();
}