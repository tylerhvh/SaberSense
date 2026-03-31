// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Loaders;
using SaberSense.Profiles;
using SaberSense.Profiles.SaberAsset;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SaberSense.Catalog;

public sealed class SaberCatalog : IDisposable, IAsyncLoadable
{
    private readonly List<string> _externalSearchPaths = [];
    public IReadOnlyList<string> ExternalSearchPaths => _externalSearchPaths;

    public Task<SaberAssetEntry?> this[string relativePath] => ResolveEntryAsync(relativePath);
    public Task<SaberAssetEntry?> this[AssetPreview preview] => ResolveEntryAsync(preview.RelativePath);

    public Task? CurrentTask => _scanGuard.IsCompleted ? null : _scanTask;

    private readonly IModLogger _log;
    private readonly IEnumerable<ISaberLoader> _loaders;
    private readonly SaberAssetBuilder _saberParser;
    private readonly AppPaths _dirs;
    private readonly IMessageBroker _broker;
    private readonly PinTracker _pins;
    private readonly string _dbPath;
    private PreviewDatabase? _db;

    private readonly ConcurrentDictionary<string, AssetPreview> _previews = new();
    private readonly ConcurrentDictionary<string, SaberAssetEntry> _loadedEntries = new();
    private readonly AsyncOnce _scanGuard = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();
    private readonly SemaphoreSlim _scanPause = new(1, 1);
    private Task? _scanTask;

    internal Action<int, int>? OnScanProgress;

    private SaberCatalog(
        IModLogger log,
        SaberAssetBuilder saberParser,
        AppPaths dirs,
        List<ISaberLoader> loaders,
        IMessageBroker broker,
        PinTracker pins)
    {
        _log = log.ForSource(nameof(SaberCatalog));
        _dirs = dirs;
        _loaders = loaders;
        _saberParser = saberParser;
        _broker = broker;
        _pins = pins;
        _dbPath = Path.Combine(_dirs.DataRoot.FullName, "saber_cache.bin");
    }

    public void Dispose()
    {
        PurgeAll();
        _db?.Dispose();
        _scanPause.Dispose();

        foreach (var sem in _refreshLocks.Values) sem.Dispose();
        _refreshLocks.Clear();
    }

    public void DiscoverExternalFolders()
    {
        _externalSearchPaths.Clear();
        foreach (var dir in _dirs.SaberRoot.GetDirectories("*", SearchOption.AllDirectories))
        {
            var cleanPath = AssetPaths.RemoveRootPrefix(AssetPaths.MakeRelative(dir.FullName));
            var sepIndex = cleanPath.IndexOf(Path.DirectorySeparatorChar);
            if (sepIndex is < 0) continue;
            _externalSearchPaths.Add(cleanPath[(sepIndex + 1)..]);
        }
    }

    public async Task<SaberAssetEntry?> ResolveEntryAsync(string relativePath)
    {
        if (_loadedEntries.TryGetValue(relativePath, out var entry)) return entry;
        return await InflateEntryAsync(relativePath);
    }

    public SaberAssetEntry? TryGetLoaded(string relativePath)
        => _loadedEntries.TryGetValue(relativePath, out var entry) ? entry : null;

    public Task<SaberAssetEntry?> ResolveEntryByPreviewAsync(AssetPreview preview) =>
        this[preview.RelativePath];

    internal Task PreparePreviewsAsync() => ScanAllPreviewsAsync();

    public async Task<bool> AddPreviewAsync(string relativePath)
    {
        if (_previews.ContainsKey(relativePath)) return false;

        var ext = Path.GetExtension(relativePath);
        ISaberLoader? loader = null;
        foreach (var l in _loaders)
            if (string.Equals(l.HandledExtension, ext, StringComparison.OrdinalIgnoreCase))
            { loader = l; break; }
        if (loader is null) return false;

        await ExtractAndStorePreviewAsync(loader, relativePath);
        return _previews.ContainsKey(relativePath);
    }

    public async Task ScanAllPreviewsAsync()
    {
        await _scanGuard.RunOnceAsync(async () =>
        {
            _db = new(_dbPath, _log);
            try { _db.Open(); }
            catch (Exception ex) { _log.Error($"Failed to open preview database: {ex}"); }
            DiscoverExternalFolders();

            foreach (var loader in _loaders)
            {
                _scanTask = ExecuteScanAsync(loader, true);
                await _scanTask;
            }
            _db.Save();
            _broker.Publish(new ScanCompleteMsg());
        });
    }

    public AssetPreview? FindPreviewForEntry(SaberAssetEntry entry)
    {
        var key = entry.LeftPiece!.Asset.RelativePath;
        return _previews.TryGetValue(key, out var preview) ? preview : null;
    }

    public IEnumerable<AssetPreview> EnumeratePreviewsByTag(AssetTypeTag tag) =>
        _previews.Values.Where(p => p.TypeTag.Equals(tag));

    public void PersistPreview(AssetPreview preview)
    {
        try { _db?.UpsertPreview(preview.ToRow()); }
        catch (Exception ex) { _log.Warn($"Failed to persist preview: {ex.Message}"); }
    }

    public void FlushPreviewCache()
    {
        try { _db?.Save(); }
        catch (Exception ex) { _log.Warn($"Failed to flush preview cache: {ex.Message}"); }
    }

    public void RegisterDefaultSaberEntry(SaberAssetEntry entry)
    {
        if (entry is null) return;
        _loadedEntries.TryAdd(DefaultSaberProvider.DefaultSaberPath, entry);
    }

    public void ShowDefaultSaberPreview(AssetPreview preview)
    {
        if (preview is null) return;
        _previews.TryAdd(DefaultSaberProvider.DefaultSaberPath, preview);
    }

    public void HideDefaultSaberPreview()
    {
        _previews.TryRemove(DefaultSaberProvider.DefaultSaberPath, out _);
    }

    private static bool IsDefaultSaber(string path) =>
        string.Equals(path, DefaultSaberProvider.DefaultSaberPath, StringComparison.Ordinal);

    public void PurgeAll()
    {
        foreach (var kvp in _loadedEntries)
        {
            if (!IsDefaultSaber(kvp.Key)) kvp.Value.Dispose();
        }
        _loadedEntries.Clear();
        foreach (var kvp in _previews)
        {
            if (!IsDefaultSaber(kvp.Key)) kvp.Value.Dispose();
        }
        _previews.Clear();
    }

    public void UnloadSpecific(string path)
    {
        if (IsDefaultSaber(path)) return;
        bool changed = false;
        if (_loadedEntries.TryRemove(path, out var entry))
        {
            entry.Dispose();
            changed = true;
        }
        if (_previews.TryRemove(path, out var preview))
        {
            preview.Dispose();
            changed = true;
        }
        if (changed)
            _broker?.Publish(new SettingsChangedMsg());
    }

    public async Task RefreshSpecificAsync(string path)
    {
        if (IsDefaultSaber(path)) return;

        var sem = _refreshLocks.GetOrAdd(path, _ => new(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_loadedEntries.TryGetValue(path, out var existing) && !existing.IsAssetStale)
                return;

            byte[]? rescuedCoverBytes = null;
            try { rescuedCoverBytes = _db!.GetPreview(path)?.CoverBytes; }
            catch (Exception ex) { _log.Debug($"Cover rescue failed for '{path}' (will regenerate): {ex.Message}"); }

            UnloadSpecific(path);

            await InflateEntryAsync(path);
            await GenerateAndStorePreviewAsync(path, rescuedCoverBytes);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task RefreshAllAsync()
    {
        PurgeAll();
        _scanGuard.Reset();

        await ScanAllPreviewsAsync();
    }

    private async Task ExecuteScanAsync(ISaberLoader loader, bool forceGeneration)
    {
        var timer = new PerfTimer("Scanning Saber Catalog");
        var pendingPaths = new List<string>();
        int discovered = 0;

        await foreach (var discovery in loader.DiscoverAsync(_dirs))
        {
            discovered++;

            if (_previews.ContainsKey(discovery.RelativePath)) continue;

            if (_db!.HasCurrentPreview(discovery.RelativePath))
            {
                var row = _db.GetPreview(discovery.RelativePath);
                if (row is not null)
                {
                    var preview = new AssetPreview(row);
                    ApplyPinState(preview, discovery.RelativePath);
                    if (InjectSiblingCoverIfNeeded(preview))
                        _db.UpsertPreview(preview.ToRow());

                    _previews.TryAdd(discovery.RelativePath, preview);
                    continue;
                }
            }

            if (forceGeneration)
                pendingPaths.Add(discovery.RelativePath);
        }

        int completed = discovered - pendingPaths.Count;
        OnScanProgress?.Invoke(completed, discovered);

        const int batchSize = 8;
        for (int i = 0; i < pendingPaths.Count; i += batchSize)
        {
            var batch = pendingPaths.GetRange(i, Math.Min(batchSize, pendingPaths.Count - i));
            await Task.WhenAll(batch.Select(path => ExtractAndStorePreviewAsync(loader, path)));
            completed += batch.Count;
            OnScanProgress?.Invoke(completed, discovered);
        }

        if (pendingPaths.Count > 0)
            _db!.Save();

        timer.Print(_log);
    }

    private async Task ExtractAndStorePreviewAsync(ISaberLoader loader, string relativePath)
    {
        if (_previews.ContainsKey(relativePath)) return;

        await _scanPause.WaitAsync();
        _scanPause.Release();

        try
        {
            var data = await loader.ExtractPreviewAsync(relativePath);
            if (data is null) return;

            var preview = new AssetPreview(relativePath, data.Value);
            ApplyPinState(preview, relativePath);
            InjectSiblingCoverIfNeeded(preview);

            _db!.UpsertPreview(preview.ToRow());
            _previews.TryAdd(relativePath, preview);
        }
        catch (Exception ex)
        {
            _log.Warn($"Preview extraction failed for {relativePath}: {ex.Message}");
        }
    }

    internal async Task<SaberAssetEntry?> GenerateAndStorePreviewAsync(
        string relativePath, byte[]? rescuedCoverBytes = null)
    {
        if (_previews.ContainsKey(relativePath)) return null;

        var entry = await this[relativePath];
        if (entry is null) return null;

        var preview = new AssetPreview(relativePath, entry, entry.TypeTag);
        ApplyPinState(preview, relativePath);

        if (preview.CoverImage == null && rescuedCoverBytes is { Length: > 0 })
            preview.InjectCoverBytes(rescuedCoverBytes);

        try
        {
            _db!.UpsertPreview(preview.ToRow());
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to write preview to database: {ex}");
        }

        _previews.TryAdd(relativePath, preview);
        return entry;
    }

    private async Task<SaberAssetEntry?> InflateEntryAsync(string relativePath)
    {
        await _scanPause.WaitAsync();
        try
        {
            return await InflateEntryCore(relativePath);
        }
        finally
        {
            _scanPause.Release();
        }
    }

    private async Task<SaberAssetEntry?> InflateEntryCore(string relativePath)
    {
        var ext = Path.GetExtension(relativePath);
        var loader = _loaders.FirstOrDefault(l => string.Equals(l.HandledExtension, ext, StringComparison.OrdinalIgnoreCase));
        if (loader is null) return null;

        var rawAsset = await loader.LoadAsync(relativePath);
        if (rawAsset is null) return null;

        var entry = _saberParser.ParseAsset(rawAsset);
        if (entry is not null)
        {
            _loadedEntries.TryAdd(relativePath, entry);
            _broker?.Publish(new SaberLoadedMsg(entry));
        }
        return entry;
    }

    private void ApplyPinState(AssetPreview preview, string relativePath)
        => preview.IsPinned = _pins.Contains(relativePath);

    private bool InjectSiblingCoverIfNeeded(AssetPreview preview)
    {
        if (preview.CoverImage != null || string.IsNullOrEmpty(preview.ContentHash))
            return false;
        var siblingCover = _db!.FindCoverByContentHash(preview.ContentHash);
        if (siblingCover is null) return false;
        preview.InjectCoverBytes(siblingCover);
        return true;
    }
}