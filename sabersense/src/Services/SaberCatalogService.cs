// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.Profiles;
using SaberSense.Profiles.SaberAsset;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Services;

internal sealed class SaberCatalogService(
    SaberCatalog catalog,
    SaberLoadout saberSet,
    PreviewSession previewSession,
    IMessageBroker broker,
    IModLogger log,
    SaberSense.Core.PinTracker pins,
    ConfigManager configManager)
{
    private readonly IModLogger _log = log.ForSource(nameof(SaberCatalogService));

    private bool _isReloading;

    public bool IsReloading => _isReloading;

    public void SetPinned(SaberAssetEntry entry, bool isOn)
    {
        if (entry is null) return;
        entry.SetPinned(isOn);

        if (catalog is not null)
        {
            var meta = catalog.FindPreviewForEntry(entry);
            meta?.SetPinned(isOn);
        }

        pins.Toggle(entry.LeftPiece!.Asset!.RelativePath);
    }

    public async Task<bool> ReloadCurrentAsync(SaberAssetEntry entry)
    {
        if (entry is null || _isReloading || saberSet is null || catalog is null) return false;
        _isReloading = true;
        try
        {
            previewSession.ResetEditorReady();
            previewSession?.WipePreviews();
            await catalog.RefreshSpecificAsync(entry.LeftPiece!.Asset!.RelativePath);
            await configManager.InitializeLoadoutAsync();

            if (saberSet!.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null)
                previewSession!.SelectRestoredEntry(sa!.OwnerEntry);
            previewSession!.SignalEditorReady();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Error during reload: {ex}");
            previewSession.SignalEditorReady();
            return false;
        }
        finally
        {
            _isReloading = false;
        }
    }

    public async Task HandleFileChangeAsync(string fullPath, Core.Loaders.FileChangeKind kind,
        Func<Task> onListChanged)
    {
        if (catalog is null) return;

        var relativePath = Core.Utilities.AssetPaths.MakeRelative(fullPath);
        var current = previewSession?.ActiveEntry;
        bool isActive = current is not null && IsPathMatch(fullPath, current.LeftPiece!.Asset!.RelativePath);

        _log.Debug($"HandleFileChange: kind={kind} path='{relativePath}' isActive={isActive}");

        switch (kind)
        {
            case Core.Loaders.FileChangeKind.Created:

                var added = await catalog.AddPreviewAsync(relativePath);
                if (added)
                {
                    broker?.Publish(new SettingsChangedMsg());
                    if (onListChanged is not null) await onListChanged();
                }
                break;

            case Core.Loaders.FileChangeKind.Modified:
                if (isActive)
                {
                    await ReloadCurrentAsync(current!);
                    if (onListChanged is not null) await onListChanged();
                }

                break;

            case Core.Loaders.FileChangeKind.Deleted:
                if (isActive)
                {
                    previewSession?.WipePreviews();
                }
                catalog.UnloadSpecific(relativePath);

                if (onListChanged is not null) await onListChanged();
                break;
        }
    }

    private static bool IsPathMatch(string fullPath, string relativePath)
    {
        var normalizedFull = fullPath.Replace('\\', '/');
        var normalizedRel = relativePath.Replace('\\', '/');
        return normalizedFull.EndsWith(normalizedRel, StringComparison.OrdinalIgnoreCase);
    }

    public void ApplyTrailSelection(SaberAssetEntry entry, Profiles.TrailSettings? trailModel,
        List<SaberSense.Rendering.SaberTrailMarker> trailList, LiveSaber? activeSaber)
    {
        if (entry?.LeftPiece is not SaberAssetDefinition leftModel) return;

        ApplyTrailToSnapshot(saberSet.Left, leftModel, trailModel, trailList, activeSaber);

        if (entry.RightPiece is SaberAssetDefinition rightModel)
            ApplyTrailToSnapshot(saberSet.Right, rightModel, trailModel, trailList, activeSaber: null);

        broker?.Publish(new SaberSense.Core.Messaging.PreviewSaberChangedMsg(entry));
    }

    private static void ApplyTrailToSnapshot(SaberProfile profile, SaberAssetDefinition model,
        Profiles.TrailSettings? trailModel, List<SaberSense.Rendering.SaberTrailMarker> trailList,
        LiveSaber? activeSaber)
    {
        if (profile?.Snapshot is null) return;

        var snapshotTrail = profile.Snapshot.TrailSettings;

        if (trailModel is null)
        {
            var td = activeSaber?.GetTrailLayout().Primary;
            td?.RevertMaterialForSaberAsset(model);
            var tm = model.ExtractTrail(false);
            if (tm is not null)
            {
                profile.Snapshot.TrailSettings = tm.Clone();
            }
        }
        else
        {
            if (snapshotTrail is null)
            {
                snapshotTrail = new Profiles.TrailSettings(
                    new MaterialHandle(null), 12, 0.5f, Vector3.zero, 0f, TextureWrapMode.Clamp)
                { OriginTrails = trailList };
                snapshotTrail.CloneUserSettings(trailModel);
                snapshotTrail.Material!.RefreshSnapshot(false);
                profile.Snapshot.TrailSettings = snapshotTrail;
            }
            else
            {
                snapshotTrail.CloneUserSettings(trailModel);
                snapshotTrail.OriginTrails = trailList;
            }
        }
    }
}