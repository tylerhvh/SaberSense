// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using AssetBundleLoadingTools.Utilities;
using SaberSense.Catalog;
using SaberSense.Catalog.Data;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Loaders;

internal static class SaberBundleFormat
{
    public const string RootPrefabName = "_CustomSaber";
}

public readonly record struct PreviewData(
    string DisplayName,
    string CreatorName,
    Sprite? CoverSprite,
    bool IsSPICompatible,
    AssetTypeTag TypeTag,
    long FileSize,
    string FileLastModified,
    string? ContentHash);

public interface ISaberLoader
{
    string HandledExtension { get; }
    IAsyncEnumerable<SaberRoute> DiscoverAsync(AppPaths dirs);
    Task<LoadedBundle?> LoadAsync(string relativePath);

    Task<PreviewData?> ExtractPreviewAsync(string relativePath);
}

public readonly record struct SaberRoute
{
    public readonly string FullPath;
    public readonly string RelativePath;
    public readonly string SubFolder;

    public SaberRoute(string fullPath)
    {
        FullPath = fullPath;
        RelativePath = AssetPaths.MakeRelative(fullPath);
        SubFolder = AssetPaths.GetSubfolderPath(RelativePath);
    }
}

internal sealed class SaberBundleLoader(SaberBundleParser parser, IModLogger log) : ISaberLoader
{
    private readonly IModLogger _log = log.ForSource(nameof(SaberBundleLoader));

    public static event Action<string, float>? OnLoadProgress;

    public string HandledExtension => ".saber";

    public async IAsyncEnumerable<SaberRoute> DiscoverAsync(AppPaths dirs)
    {
        await Task.CompletedTask;
        var files = dirs.SaberRoot.EnumerateFiles("*.saber", SearchOption.AllDirectories);
        foreach (var file in files)
            yield return new SaberRoute(file.FullName);
    }

    public async Task<LoadedBundle?> LoadAsync(string relativePath)
    {
        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        OnLoadProgress?.Invoke("Parsing saber data...", 0.05f);
        var parseResult = await Task.Run(() => parser.Parse(fullPath));

        var materialScope = new MaterialSnapshotScope();

        OnLoadProgress?.Invoke("Loading asset bundle...", 0.15f);
        var result = await BundleLoader.LoadFromFileAsync<GameObject>(fullPath, SaberBundleFormat.RootPrefabName);
        if (result is null)
        {
            return null;
        }

        var bundleOwnedMaterials = materialScope.GetNewMaterials();

        await Task.Yield();

        OnLoadProgress?.Invoke("Injecting components...", 0.40f);
        if (parseResult is not null)
        {
            var coverSprite = TryCreateCoverFromParsedData(parseResult.CoverImage)
                              ?? TryLoadCoverSprite(result.Value.Bundle);

            var metadata = parseResult.Metadata;
            if (coverSprite != null && metadata.CoverImage == null)
            {
                metadata = new(metadata.Name, metadata.Author, metadata.Description, coverSprite);
            }

            PrefabComponentInjector.InjectDescriptor(result.Value.Asset, metadata);
            PrefabComponentInjector.InjectTrails(result.Value.Asset, parseResult.Trails, parseResult, result.Value.Bundle, bundleOwnedMaterials);

            if (parseResult.Modifiers.Count is > 0)
                PrefabComponentInjector.InjectModifiers(result.Value.Asset, parseResult.Modifiers, parseResult);

            if (parseResult.SpringBones.Count is > 0 || parseResult.SpringColliders.Count is > 0)
                PrefabComponentInjector.InjectSpringBones(result.Value.Asset, parseResult);

            PrefabComponentInjector.MirrorAnimations(result.Value.Asset);
        }

        await Task.Yield();

        OnLoadProgress?.Invoke("Repairing shaders...", 0.50f);
        var spiCompatible = await ShaderBindingFixer.FixAsync(result.Value.Asset);

        OnLoadProgress?.Invoke("Finalizing...", 0.65f);
        var contentHash = await Task.Run(() => ContentHasher.TryCompute(fullPath));

        var bundle = new LoadedBundle(relativePath, result.Value.Asset, result.Value.Bundle)
        {
            IsSPICompatible = spiCompatible,
            ParsedBounds = parseResult?.ParsedBounds,
            ContentHash = contentHash,
            ParseResult = parseResult?.HasEvents == true ? parseResult : null
        };

        return bundle;
    }

    private static Sprite? TryCreateCoverFromParsedData(CoverImageData? coverData)
        => coverData is not null ? SpriteFactory.FromRawGPU(coverData) : null;

    private static Sprite? TryLoadCoverSprite(AssetBundle? bundle)
    {
        if (bundle == null) return null;

        try
        {
            var sprites = bundle.LoadAllAssets<Sprite>();
            if (sprites is { Length: > 0 })
                return sprites[0];

            var textures = bundle.LoadAllAssets<Texture2D>();
            if (textures is not null)
            {
                foreach (var tex in textures)
                {
                    if (tex.width >= 32 && tex.height >= 32 && tex.width <= 1024 && tex.height <= 1024)
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("SaberBundleLoader").Warn($"Failed to load cover sprite: {ex.Message}");
        }

        return null;
    }

    public Task<PreviewData?> ExtractPreviewAsync(string relativePath)
    {
        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<PreviewData?>(null);

        try
        {
            var parseResult = parser.Parse(fullPath);
            if (parseResult is null)
                return Task.FromResult<PreviewData?>(null);

            var coverSprite = parseResult.CoverImage is not null ? SpriteFactory.FromRawGPU(parseResult.CoverImage) : null;

            var fileInfo = new FileInfo(fullPath);

            var contentHash = ContentHasher.TryCompute(fullPath);

            var preview = new PreviewData(
                parseResult.Metadata.Name ?? "Custom Saber",
                parseResult.Metadata.Author ?? "Unknown",
                coverSprite,
                true,
                AssetTypeTag.SaberAsset,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.ToString("O"),
                contentHash);

            return Task.FromResult<PreviewData?>(preview);
        }
        catch (Exception ex)
        {
            _log?.Warn($"Preview extraction failed for {relativePath}: {ex.Message}");
            return Task.FromResult<PreviewData?>(null);
        }
    }
}

internal static class ShaderBindingFixer
{
    private static readonly System.Lazy<System.Reflection.MethodInfo?> _spiCheckMethod = new(() =>
    {
        try
        {
            var shaderReaderType = System.Type.GetType(
                "AssetBundleLoadingTools.Utilities.ShaderReader, AssetBundleLoadingTools");
            return shaderReaderType?.GetMethod(
                "IsSinglePassInstancedSupported",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("ShaderFixer").Warn($"Error resolving SPI check method: {ex.Message}");
            return null;
        }
    });

    private const int ShaderBatchSize = 5;

    public static async Task<bool> FixAsync(GameObject saberObject)
    {
        bool spiCompatible = true;
        try
        {
            var materials = ShaderRepair.GetMaterialsFromGameObjectRenderers(saberObject);
            var trailMaterials = saberObject.GetComponentsInChildren<SaberTrailMarker>(true)
                .Select(t => t.TrailMaterial)
                .Where(m => m != null && !materials.Contains(m))!;
            materials.AddRange(trailMaterials!);

            if (_spiCheckMethod.Value is not null)
            {
                var uniqueShaders = materials
                    .Where(m => m != null && m.shader != null)
                    .Select(m => m.shader).Distinct().ToList();
                foreach (var shader in uniqueShaders)
                {
                    try
                    {
                        bool supported = (bool)_spiCheckMethod.Value.Invoke(null, [shader]);
                        if (!supported)
                        {
                            spiCompatible = false;
                            break;
                        }
                    }
                    catch (Exception ex) { ModLogger.ForSource("SaberBundleLoader").Debug($"SPI shader check failed: {ex.Message}"); spiCompatible = false; break; }
                }
            }

            var missingNames = new List<string>();
            for (int i = 0; i < materials.Count; i += ShaderBatchSize)
            {
                int count = Math.Min(ShaderBatchSize, materials.Count - i);
                var batch = materials.GetRange(i, count);
                var repairResult = ShaderRepair.FixShadersOnMaterials(batch);
                if (!repairResult.AllShadersReplaced)
                    missingNames.AddRange(repairResult.MissingShaderNames);

                if (i + ShaderBatchSize < materials.Count)
                    await Task.Yield();
            }
            if (missingNames.Count is > 0)
            {
                ModLogger.Warn($"Some shaders could not be repaired: {string.Join(", ", missingNames)}");
            }
        }

        catch (Exception ex)
        {
            ModLogger.Error($"Failed to repair shaders on saber: {ex}");
            spiCompatible = false;
        }

        return spiCompatible;
    }
}