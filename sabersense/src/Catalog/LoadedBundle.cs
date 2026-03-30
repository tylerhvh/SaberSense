// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.Catalog;

public enum AssetOrigin
{
    Disk,

    External,

    Generated
}

public sealed class LoadedBundle : IDisposable
{
    public readonly AssetBundle? Bundle;
    public readonly string FileExtension;
    public readonly string FileName;
    public readonly string BaseName;
    public readonly string RelativePath;
    public readonly string DirectoryName;
    public readonly AssetOrigin Origin;
    public bool ExistsOnDisk => Origin == AssetOrigin.Disk;
    public GameObject Prefab { get; internal set; }
    public bool IsSPICompatible { get; set; } = true;

    public string? ContentHash { get; set; }

    public (float minZ, float maxZ)? ParsedBounds { get; set; }

    public Data.SaberParseResult? ParseResult { get; set; }

    public bool IsPrefabStale => Prefab == null;

    public LoadedBundle(string relativePath, GameObject prefab, AssetBundle? bundle, AssetOrigin origin = AssetOrigin.Disk)
    {
        RelativePath = relativePath;
        Origin = origin;
        FileName = Path.GetFileName(RelativePath);
        BaseName = Path.GetFileNameWithoutExtension(FileName);
        FileExtension = Path.GetExtension(FileName);
        DirectoryName = AssetPaths.GetSubfolderPath(relativePath);
        Prefab = prefab;
        Bundle = bundle;
    }

    public void Unload()
    {
        if (Bundle != null) Bundle.Unload(false);
        if (Prefab) Object.Destroy(Prefab);
    }

    public void Dispose() => Unload();
}

internal sealed class CachedTexture : IDisposable
{
    public Sprite Sprite => _sprite ??= GenerateSprite();

    public bool IsActive { get; internal set; }
    public string Identifier { get; internal set; }
    public AssetSource Source { get; internal set; }
    public string AbsolutePath { get; internal set; }
    public Texture2D Texture { get; internal set; }

    private Sprite? _sprite;

    public CachedTexture(string identifier, string absolutePath, Texture2D texture, AssetSource source)
    {
        Identifier = identifier;
        AbsolutePath = absolutePath;
        Texture = texture;
        Source = source;
        Texture.wrapMode = ResolveWrapMode(identifier, texture);
    }

    private static TextureWrapMode ResolveWrapMode(string name, Texture2D tex)
    {
        if (tex.wrapMode == TextureWrapMode.Clamp)
            return TextureWrapMode.Clamp;

        if (name.IndexOf("_clamp", StringComparison.OrdinalIgnoreCase) is >= 0)
            return TextureWrapMode.Clamp;

        return tex.wrapMode;
    }

    public void Dispose()
    {
        if (Texture) Object.Destroy(Texture);
        if (_sprite) Object.Destroy(_sprite);
    }

    private Sprite GenerateSprite() => SpriteFactory.FromTexture(Texture)!;

    public void DisposeIfUnused()
    {
        if (!IsActive) Dispose();
    }
}