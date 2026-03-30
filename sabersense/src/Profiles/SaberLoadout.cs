// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Data;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Profiles.SaberAsset;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zenject;

namespace SaberSense.Profiles;

public class SaberLoadout
{
    public SaberProfile Left { get; private set; }

    public SaberProfile Right { get; private set; }

    public bool IsEmpty => Left.IsBlank && Right.IsBlank;

    public SaberProfile this[SaberHand hand] => hand == SaberHand.Left ? Left : Right;

    private readonly SaberCatalog _catalog;
    private readonly IModLogger _log;
    private readonly Serializer _serializer;

    private readonly Dictionary<string, JObject> _saberSettings = [];

    internal void ClearSaberSettings() => _saberSettings.Clear();

    internal ModSettings Settings { get; }

    internal SaberLoadout(
        [Inject(Id = SaberHand.Left)] SaberProfile leftProfile,
        [Inject(Id = SaberHand.Right)] SaberProfile rightProfile,
        SaberCatalog catalog,
        ModSettings settings,
        Serializer serializer,
        IModLogger log)
    {
        _catalog = catalog;
        _log = log.ForSource(nameof(SaberLoadout));
        _serializer = serializer;
        Settings = settings;
        Left = leftProfile;
        Right = rightProfile;
    }

    internal async Task EquipEntryAsync(SaberAssetEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        _log.Debug($"EquipEntry: '{entry.DisplayName}' left='{entry.LeftPiece?.Asset?.RelativePath}' right='{entry.RightPiece?.Asset?.RelativePath}'");

        if (!_isLoadingConfig)
        {
            await SaveCurrentSettingsAsync(Left);
            await SaveCurrentSettingsAsync(Right);
        }

        Left.ApplyAssetEntry(entry);
        Right.ApplyAssetEntry(entry);

        if (!_isLoadingConfig)
        {
            await LoadSaberSettingsAsync(Left, entry.LeftPiece);
            await LoadSaberSettingsAsync(Right, entry.RightPiece);
        }
    }

    private async Task SaveCurrentSettingsAsync(SaberProfile profile)
    {
        if (profile.Snapshot is null) return;
        if (!profile.TryGetSaberAsset(out var sa)) return;
        var path = sa!.Asset?.RelativePath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var obj = new JObject();
            await profile.Snapshot.WriteTo(obj, _serializer);
            _saberSettings[path!] = obj;
            _log.Debug($"SaveCurrentSettings: saved '{path}' ({profile.Hand})");
        }
        catch (Exception ex)
        {
            _log.Error($"SaveCurrentSettings failed for '{path}': {ex.Message}");
        }
    }

    private async Task LoadSaberSettingsAsync(SaberProfile profile, PieceDefinition? piece)
    {
        var path = piece?.Asset?.RelativePath;
        var def = piece as SaberAssetDefinition;

        profile.Snapshot = ConfigSnapshot.SeedFromDefinition(def!);

        if (!string.IsNullOrEmpty(path) && _saberSettings.TryGetValue(path!, out var saved))
        {
            try
            {
                await profile.Snapshot.ReadFrom(saved, _serializer);
                _log.Debug($"LoadSaberSettings: loaded '{path}' ({profile.Hand})");
            }
            catch (Exception ex)
            {
                _log.Error($"LoadSaberSettings failed for '{path}': {ex.Message}");
            }
            return;
        }

        _log.Debug($"LoadSaberSettings: fresh defaults for '{path}' ({profile.Hand})");
    }

    public async Task EquipFromPreview(AssetPreview preview)
    {
        if (preview is null) return;
        var entry = await _catalog.ResolveEntryByPreviewAsync(preview);
        if (entry is null)
        {
            _log.Warn($"EquipFromPreview: could not resolve entry for preview '{preview.DisplayName}'");
            return;
        }
        await EquipEntryAsync(entry);
    }

    public void SyncDimensions(SaberProfile source)
    {
        source.PropagateChanges();
        var target = source == Left ? Right : Left;

        target.Scale = new() { Length = source.Scale.Length, Width = source.Scale.Width };

        if (source.Snapshot?.TrailSettings is not null && target.Snapshot is not null)
        {
            target.Snapshot.TrailSettings ??= new();
            target.Snapshot.TrailSettings.CloneFrom(source.Snapshot.TrailSettings);
        }
    }

    private bool _isLoadingConfig;

    public IDisposable ConfigLoadScope()
    {
        _isLoadingConfig = true;
        return new ConfigLoadGuard(this);
    }

    private readonly struct ConfigLoadGuard : IDisposable
    {
        private readonly SaberLoadout _owner;
        public ConfigLoadGuard(SaberLoadout owner) => _owner = owner;
        public void Dispose() => _owner._isLoadingConfig = false;
    }

    internal void ResetAllModifierBindings()
    {
        ResetModifiersForProfile(Left);
        ResetModifiersForProfile(Right);
    }

    private static void ResetModifiersForProfile(SaberProfile profile)
    {
        foreach (PieceDefinition piece in profile.Pieces)
        {
            if (piece?.ComponentModifiers is null) continue;
            foreach (var b in piece.ComponentModifiers.AllBindings()) b.Reset();
        }

        if (profile.Snapshot is not null)
            profile.Snapshot.ModifierState = null;
    }

    public async Task FromJson(JObject obj, Serializer serializer)
    {
        try
        {
            ResetAllModifierBindings();

            _log.Debug($"FromJson: clearing old state (Left pieces={Left.Pieces.Count}, Right pieces={Right.Pieces.Count})");
            _saberSettings.Clear();
            Left.Pieces.Clear();
            Right.Pieces.Clear();
            Left.Snapshot = null;
            Right.Snapshot = null;
            Left.Scale = SaberScale.Unit;
            Right.Scale = SaberScale.Unit;
            Left.Trail = null;
            Right.Trail = null;

            if (obj.TryGetValue(nameof(Left), out var leftToken))
                await SaberProfileCodec.ReadInto(Left, (JObject)leftToken, serializer);

            if (obj.TryGetValue(nameof(Right), out var rightToken))
                await SaberProfileCodec.ReadInto(Right, (JObject)rightToken, serializer);

            if (obj.TryGetValue("SaberSettings", out var ssToken) && ssToken is JObject ssObj)
            {
                foreach (var prop in ssObj.Properties())
                    _saberSettings[prop.Name] = (JObject)prop.Value;
                _log.Debug($"FromJson: loaded {_saberSettings.Count} saber setting(s)");
            }

            if (Left.TryGetSaberAsset(out var diagSa))
                _log.Info($"FromJson: After ReadInto: Left piece = '{diagSa?.OwnerEntry?.DisplayName}'");
            else
                _log.Info($"FromJson: After ReadInto: Left has no saber asset (Pieces count = {Left.Pieces.Count})");

            if (obj.TryGetValue("Settings", out var settingsToken) && settingsToken is JObject settingsObj)
            {
                Settings.ResetToDefaults();

                var restored = settingsObj.ToObject<ModSettings>(serializer.Json)!;
                ModSettingsCopier.CopyAll(restored, Settings);

                Settings.RaisePropertyChanged(null);
                _log.Debug("FromJson: settings restored from preset");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to restore loadout:\n{ex}");
            throw;
        }
    }

    public async Task<JToken> ToJson(Serializer serializer)
    {
        await SaveCurrentSettingsAsync(Left);
        await SaveCurrentSettingsAsync(Right);

        var ssObj = new JObject();
        foreach (var kv in _saberSettings)
            ssObj[kv.Key] = kv.Value;

        return new JObject
        {
            { nameof(Left), await SaberProfileCodec.Write(Left, serializer) },
            { nameof(Right), await SaberProfileCodec.Write(Right, serializer) },
            { "Settings", JObject.FromObject(Settings, serializer.Json) },
            { "SaberSettings", ssObj }
        };
    }
}