// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities.Async;
using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Data;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SaberSense.Services;

internal readonly record struct ConfigInfo(string Name, bool IsDefault, bool IsActive);

internal sealed class ConfigManager : IDisposable
{
    private const string FileExtension = ".sabersense";
    private const string DefaultConfigName = "default";

    private readonly SaberLoadout _loadout;
    private readonly Serializer _serializer;
    private readonly InternalConfig _internalConfig;
    private readonly SessionController _session;
    private readonly string _configDir;
    private readonly IMessageBroker _broker;
    private readonly AssetRecoveryService _recovery;
    private readonly IModLogger _log;

    private FileSystemWatcher? _fsw;
    private volatile bool _configListStale;
    private int _refreshPosted;

    private int _isLoading;
    private int _isSaving;

    public Task? CurrentTask { get; private set; }

    public event Action? OnConfigsChanged;

    public string ConfigDirectory => _configDir;

    private string ActiveConfigPath => GetFilePath(_internalConfig.ActiveConfigName ?? DefaultConfigName);

    public ConfigManager(
        SaberLoadout loadout,
        Serializer serializer,
        InternalConfig internalConfig,
        SaberCatalog catalog,
        LoadoutCoordinator coordinator,
        SessionController session,
        AppPaths paths,
        IMessageBroker broker,
        IModLogger log)
    {
        _loadout = loadout;
        _serializer = serializer;
        _internalConfig = internalConfig;
        _session = session;
        _configDir = paths.ConfigsRoot.FullName;
        _broker = broker;
        _log = log.ForSource(nameof(ConfigManager));

        _recovery = new AssetRecoveryService(
            loadout, catalog, coordinator, log);
    }

    public async Task InitializeLoadoutAsync()
    {
        var cfgPath = ActiveConfigPath;
        _log.Debug($"InitializeLoadout: activeConfig='{_internalConfig.ActiveConfigName}' path='{cfgPath}'");

        var restorePhase = _session.Phase is SessionPhase.Editing
            ? SessionPhase.Editing : SessionPhase.Idle;

        if (!File.Exists(cfgPath))
        {
            _log.Info($"Config '{_internalConfig.ActiveConfigName}' not found -- creating with defaults.");
            await ForcePersistAsync();
            _session.TransitionTo(restorePhase);
            return;
        }

        try
        {
            var payload = ConfigEnvelope.ReadFromDisk(cfgPath);
            using (_loadout.ConfigLoadScope())
            {
                CurrentTask = _loadout.FromJson(payload, _serializer);
                await CurrentTask;
            }

            DeselectIncompatibleSabers();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to restore loadout from config file:\n{ex}");
        }
        finally
        {
            CurrentTask = null;
            _session.TransitionTo(restorePhase);
        }
    }

    public async Task ForcePersistAsync()
    {
        _log.Debug($"ForcePersist: activeConfig='{_internalConfig.ActiveConfigName}' saber={GetEquippedSaberName()}");
        var token = await _loadout.ToJson(_serializer);
        if (token is JObject obj)
        {
            var path = ActiveConfigPath;
            ConfigEnvelope.WriteToDisk(path, obj);
            _log.Info($"ForcePersist written to '{Path.GetFileName(path)}'");
        }
        else
        {
            _log.Warn("ForcePersist: ToJson returned null or non-object");
        }
    }

    public Task EnsureAssetsValidAsync() => _recovery.EnsureAssetsValidAsync();

    public void StartWatching()
    {
        if (_fsw is not null) return;
        if (!Directory.Exists(_configDir)) return;

        try
        {
            _fsw = new FileSystemWatcher(_configDir, $"*{FileExtension}")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _fsw.Created += OnFileSystemEvent;
            _fsw.Deleted += OnFileSystemEvent;
            _fsw.Renamed += OnFileSystemEvent;
            _fsw.Changed += OnFileSystemEvent;
            _fsw.Error += (_, e) => _log.Debug($"Watcher error: {e.GetException().Message}");
        }
        catch (Exception ex)
        {
            _log.Debug($"Failed to start watcher: {ex.Message}");
            _fsw?.Dispose();
            _fsw = null;
        }
    }

    public void StopWatching()
    {
        if (_fsw is null) return;

        _fsw.Created -= OnFileSystemEvent;
        _fsw.Deleted -= OnFileSystemEvent;
        _fsw.Renamed -= OnFileSystemEvent;
        _fsw.Changed -= OnFileSystemEvent;
        _fsw.EnableRaisingEvents = false;
        _fsw.Dispose();
        _fsw = null;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (_isLoading != 0 || _isSaving != 0) return;

        _configListStale = true;

        if (Interlocked.CompareExchange(ref _refreshPosted, 1, 0) == 0)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                Interlocked.Exchange(ref _refreshPosted, 0);
                if (_configListStale)
                {
                    _configListStale = false;
                    OnConfigsChanged?.Invoke();
                }
            });
        }
    }

    public async Task ValidateActiveConfigAsync()
    {
        var active = _internalConfig.ActiveConfigName ?? DefaultConfigName;
        var path = GetFilePath(active);
        _log.Debug($"ValidateActiveConfig: checking '{active}' at '{path}'");

        if (!File.Exists(path))
        {
            _log.Warn($"Active config '{active}' no longer exists -- reverting to '{DefaultConfigName}'");
            _internalConfig.ActiveConfigName = DefaultConfigName;
            _internalConfig.Save();

            var defaultPath = GetFilePath(DefaultConfigName);
            if (!File.Exists(defaultPath))
            {
                _log.Info($"Recreating missing default config at '{defaultPath}'");
                await ForcePersistAsync();
            }
        }
    }

    public List<ConfigInfo> GetConfigs()
    {
        var active = _internalConfig.ActiveConfigName ?? DefaultConfigName;
        var list = new List<ConfigInfo>();

        if (Directory.Exists(_configDir))
        {
            var files = Directory.GetFiles(_configDir, $"*{FileExtension}")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var name in files)
            {
                bool isDefault = string.Equals(name, DefaultConfigName, StringComparison.OrdinalIgnoreCase);
                list.Add(new ConfigInfo(name, IsDefault: isDefault, IsActive: string.Equals(active, name, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return list;
    }

    public async Task SaveAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = SanitizeName(name);

        var payload = await _loadout.ToJson(_serializer);
        if (payload is not JObject obj) return;

        var path = GetFilePath(name);
        System.Threading.Interlocked.Exchange(ref _isSaving, 1);
        try { ConfigEnvelope.WriteToDisk(path, obj); }
        finally { System.Threading.Interlocked.Exchange(ref _isSaving, 0); }

        _internalConfig.ActiveConfigName = name;
        _internalConfig.Save();
        _log.Info($"Saved config '{name}'");
    }

    public async Task LoadAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (System.Threading.Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
        {
            _log.Warn($"LoadAsync('{name}') skipped -- already loading");
            return;
        }
        try
        {
            var active = _internalConfig.ActiveConfigName ?? DefaultConfigName;
            _log.Debug($"LoadAsync: target='{name}' current='{active}' phase={_session.Phase}");

            var path = GetFilePath(name);
            if (!File.Exists(path))
            {
                _log.Warn($"Config file not found: {path}");
                return;
            }

            JObject payload;
            try
            {
                payload = ConfigEnvelope.ReadFromDisk(path);
                _log.Debug($"LoadAsync: read '{name}' from disk, keys=[{string.Join(", ", payload.Properties().Select(p => p.Name))}]");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to read config '{name}': {ex.Message}");
                return;
            }

            _broker?.Publish(new ConfigLoadingMsg());

            var prevPhase = _session.Phase;
            _session.TransitionTo(SessionPhase.LoadingConfig);

            try
            {
                using (_loadout.ConfigLoadScope())
                {
                    await _loadout.FromJson(payload, _serializer);
                    _internalConfig.ActiveConfigName = name;
                    _internalConfig.Save();
                    _log.Info($"Loaded config '{name}'");
                    _log.Debug($"LoadAsync: post-load saber={GetEquippedSaberName()}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to apply config '{name}': {ex.Message}");
            }
            finally
            {
                _session.TransitionTo(prevPhase is SessionPhase.Editing ? SessionPhase.Editing : SessionPhase.Idle);
            }

            using (_loadout.ConfigLoadScope())
            {
                _broker?.Publish(new ConfigLoadedMsg());
            }
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _isLoading, 0);
        }
    }

    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.Equals(name, DefaultConfigName, StringComparison.OrdinalIgnoreCase)) return false;

        var path = GetFilePath(name);
        if (!File.Exists(path)) return false;

        try
        {
            File.Delete(path);

            if (string.Equals(_internalConfig.ActiveConfigName, name, StringComparison.OrdinalIgnoreCase))
            {
                _internalConfig.ActiveConfigName = DefaultConfigName;
                _internalConfig.Save();
            }

            _log.Info($"Deleted config '{name}'");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete config '{name}': {ex.Message}");
            return false;
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        await _loadout.FromJson(new JObject(), _serializer);

        _loadout.Settings.ResetToDefaults();

        _log.Info("Reset loadout + settings to defaults (in memory -- not saved)");
        _broker?.Publish(new ConfigLoadedMsg());
    }

    public async Task<string?> ExportAsync()
    {
        try
        {
            var payload = await _loadout.ToJson(_serializer);
            if (payload is not JObject obj) return null;

            var result = ConfigEnvelope.ToClipboardString(obj);
            _log.Info($"Exported config ({result.Length} chars)");
            return result;
        }
        catch (Exception ex)
        {
            _log.Error($"Export failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ImportAsync(string clipboardData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clipboardData))
            {
                _log.Warn("Import data is empty.");
                return false;
            }

            var payload = ConfigEnvelope.FromClipboardString(clipboardData);
            _broker?.Publish(new ConfigLoadingMsg());
            using (_loadout.ConfigLoadScope())
            {
                await _loadout.FromJson(payload, _serializer);
                _broker?.Publish(new ConfigLoadedMsg());
            }
            _log.Info("Imported config (not saved -- use Save to persist)");
            return true;
        }
        catch (FormatException ex)
        {
            _log.Warn($"Invalid data: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error($"Import failed: {ex.Message}");
            return false;
        }
    }

    private void DeselectIncompatibleSabers()
    {
        if (Plugin.MultiPassEnabled) return;

        bool incompatible =
            (_loadout.Left.TryGetSaberAsset(out var leftCs) && leftCs?.OwnerEntry is not null && !leftCs.OwnerEntry.IsSPICompatible) ||
            (_loadout.Right.TryGetSaberAsset(out var rightCs) && rightCs?.OwnerEntry is not null && !rightCs.OwnerEntry.IsSPICompatible);

        if (incompatible)
        {
            _log.Info("Saved saber requires multi-pass rendering -- deselecting and using default saber.");
            _loadout.Left.Pieces.Clear();
            _loadout.Right.Pieces.Clear();
        }
    }

    private string GetFilePath(string name) => Path.Combine(_configDir, name + FileExtension);

    private static string SanitizeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    private string GetEquippedSaberName()
    {
        if (_loadout.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null)
            return sa.OwnerEntry.DisplayName;
        return "(none)";
    }

    public void Dispose()
    {
        StopWatching();
    }
}