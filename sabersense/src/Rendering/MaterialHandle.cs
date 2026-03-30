// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using System;
using UnityEngine;

namespace SaberSense.Rendering;

public class MaterialHandle : IDisposable
{
    public Material? Material
    {
        get => _material;
        set => _material = value;
    }
    private Material? _material;

    public bool IsValid => Material != null;

    public bool IsOwned => _isOwned;

    private Material? _snapshot;
    private bool _disposed;
    private readonly bool _isOwned;

    public MaterialHandle(Material? source) : this(source, true) { }

    private MaterialHandle(Material? source, bool isOwned)
    {
        _material = source;
        _isOwned = isOwned;
        _snapshot = source != null ? new Material(source) : null;
    }

    public static MaterialHandle Borrow(Material source) => new(source, false);

    public virtual void Revert()
    {
        if (_snapshot == null) return;

        if (_isOwned) _material?.TryDestroyImmediate();
        Material = new Material(_snapshot);
    }

    public void RefreshSnapshot(bool disposeOldSnapshot = true)
    {
        if (disposeOldSnapshot) _snapshot?.TryDestroyImmediate();
        _snapshot = new Material(Material);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_isOwned) _material?.TryDestroyImmediate();
        _material = null;
        _snapshot?.TryDestroyImmediate();
        _snapshot = null;
    }
}

internal sealed class RendererBoundMaterialHandle : MaterialHandle
{
    private readonly Renderer _renderer;
    private readonly int _slotIndex;

    public RendererBoundMaterialHandle(Material material, Renderer renderer, int slotIndex)
        : base(material)
    {
        _renderer = renderer;
        _slotIndex = slotIndex;
    }

    public override void Revert()
    {
        base.Revert();
        if (_renderer == null) return;
        var materials = _renderer.sharedMaterials;
        if (_slotIndex >= materials.Length) return;
        materials[_slotIndex] = Material;
        _renderer.sharedMaterials = materials;
    }
}