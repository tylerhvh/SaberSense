// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.Profiles;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Menu.Tabs;

internal sealed class SaberTabView(
    SaberSelectionController selectionController,
    SaberCatalogController catalogController,
    SaberTransformController transformController,
    PreviewController previewController,
    ModSettings pluginConfig,
    PreviewSession previewSession,
    SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
    SaberSense.Customization.SaberEditor editor,
    SaberCatalog catalog,
    IMessageBroker broker,
    IModLogger log) : IDisposable
{
    private readonly IModLogger _log = log.ForSource(nameof(SaberTabView));
    private readonly BindingScope _bindingScope = new();

    private UIScrollList _saberList = null!;
    private BaseButton _favoriteButton = null!;
    private UIMultiComboBox _grabSaberCombo = null!;
    private UIMultiComboBox _transformSabersCombo = null!;
    private UISlider _saberWidthSlider = null!;
    private UISlider _saberLengthSlider = null!;
    private UISlider _rotationSlider = null!;
    private UISlider _offsetSlider = null!;
    private float _activeSaberLength = 1f;
    private float _activeSaberOffset = 0f;

    private RectTransform _canvasRoot = null!;
    private NativeMessagePopup _messagePopup = null!;
    private UILoadingOverlay _loadingOverlay = null!;
    private GameObject? _previewWindowGO;

    private TaskCompletionSource<bool>? _spawnComplete;

    public GameObject Build(RectTransform parent, RectTransform canvasRoot,
        NativeMessagePopup messagePopup, GameObject previewWindowGO)
    {
        _canvasRoot = canvasRoot;
        _messagePopup = messagePopup;
        _loadingOverlay = new UILoadingOverlay(canvasRoot);
        _previewWindowGO = previewWindowGO;

        var tabRoot = UILayoutFactory.TabRoot("SaberTab", parent);

        var root = new HBox("SaberCols").SetParent(tabRoot);
        UnityEngine.Object.Destroy(root.GameObject.GetComponent<ContentSizeFitter>());
        root.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0);
        root.AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var listCol = new VBox("ListCol").SetParent(root.RectTransform).SetAlignment(TextAnchor.UpperLeft);
        listCol.SetPadding(0, 0, 0, 0).SetSpacing(UITheme.GroupGap)
            .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        UnityEngine.Object.Destroy(listCol.GameObject.GetComponent<ContentSizeFitter>());

        _saberList = new UIScrollList("SaberList", "Available sabers").SetParent(listCol.RectTransform);
        _saberList.AddLayoutElement(flexibleHeight: 1);
        _saberList.EnableSearch(canvasRoot);
        _saberList.OnSelect((idx, data) => ErrorBoundary.FireAndForget(SaberSelectedAsync(idx, data), _log, nameof(SaberSelectedAsync)));

        LoadSabers();

        BuildRightColumn(root.RectTransform);
        BindEvents();

        return tabRoot.gameObject;
    }

    private IDisposable? _equippedSub;
    private IDisposable? _settingsChangedSub;
    private IDisposable? _spawnedSub;
    private IDisposable? _configLoadingSub;
    private IDisposable? _configLoadedSub;
    private bool _externalLoadOverlayActive;

    private void BindEvents()
    {
        _equippedSub = broker?.Subscribe<SaberEquippedMsg>(msg => OnEquipped(msg.Entry))!;
        _spawnedSub = broker?.Subscribe<SaberPreviewInstantiatedMsg>(_ => _spawnComplete?.TrySetResult(true))!;
        SaberSense.Loaders.SaberBundleLoader.OnLoadProgress += OnLoadProgress;
        SaberSense.Core.Utilities.BundleLoader.OnAssetProgress += OnAssetProgress;
        _configLoadingSub = broker?.Subscribe<ConfigLoadingMsg>(_ =>
        {
            ErrorBoundary.FireAndForget(ShowExternalLoadOverlay(), _log, "ConfigLoadingOverlay");
        });
        _configLoadedSub = broker?.Subscribe<ConfigLoadedMsg>(_ => HideExternalLoadOverlay());
        _settingsChangedSub = broker?.Subscribe<SettingsChangedMsg>(_ =>
        {
            ErrorBoundary.FireAndForget(ShowSabers(), _log, nameof(ShowSabers));

            if (editor is not null && pluginConfig?.GrabSelections is not null)
            {
                HashSet<int> grab = [.. pluginConfig.GrabSelections];
                editor.SetGrab(grab.Contains(0), grab.Contains(1));
            }
            _previewWindowGO?.SetActive(pluginConfig?.Editor?.PreviewSaber ?? true);
        })!;
    }

    public void Refresh()
    {
        var entry = selectionController.SelectedEntry;

        if (_favoriteButton is not null)
        {
            bool pinned = entry?.IsPinned ?? false;
            _favoriteButton.Label.SetText(pinned ? "Unpin saber" : "Pin saber");
            _favoriteButton.NormalSprite = pinned ? UIGradient.AccentVert : UIGradient.BtnNormal;
            _favoriteButton.HoverSprite = pinned ? UIGradient.AccentVert : UIGradient.BtnHover;
            _favoriteButton.PressedSprite = pinned ? UIGradient.AccentVert : UIGradient.BtnPressed;
            _favoriteButton.GradientOverlay.SetSprite(_favoriteButton.NormalSprite);
            if (!pinned) _favoriteButton.Label.SetColor(Color.white);
        }

        if (entry is null) return;
        _saberWidthSlider?.SetRange(0, pluginConfig?.MaxGlobalWidth ?? 5);

        transformController.SyncFromActiveSaber();
    }

    public float ActiveSaberLength => _activeSaberLength;

    public float ActiveSaberOffset => _activeSaberOffset;

    public void OnSaberPreviewInstantiated(SaberSense.Rendering.LiveSaber liveSaber)
    {
        _activeSaberLength = liveSaber?.Profile?.Scale.Length ?? 1f;
        _activeSaberOffset = 0f;
    }

    private void OnEquipped(SaberAssetEntry entry)
    {
        if (entry is not null && _saberList is not null && catalog is not null)
        {
            var meta = catalog.FindPreviewForEntry(entry);
            if (meta is not null) _saberList.Select(meta.DisplayName, false);
        }
        HideExternalLoadOverlay();
        Refresh();
    }

    private async Task ShowExternalLoadOverlay()
    {
        if (_loadingOverlay is null) return;
        _externalLoadOverlayActive = true;
        await _loadingOverlay.ShowAsync();
    }

    private void HideExternalLoadOverlay()
    {
        if (!_externalLoadOverlayActive) return;
        _externalLoadOverlayActive = false;
        _loadingOverlay?.Hide();
    }

    private void LoadSabers() => ErrorBoundary.FireAndForget(LoadSabersAsync(), _log, nameof(LoadSabersAsync));

    private async Task LoadSabersAsync()
    {
        if (catalog is not null)
        {
            await _loadingOverlay.ShowAsync();
            _loadingOverlay.SetPhase("Scanning saber catalogue...", 0f);

            catalog.OnScanProgress = (completed, total) =>
            {
                if (total > 0)
                    _loadingOverlay.SetPhase($"Scanning saber catalogue ({completed}/{total})...",
                        (float)completed / total);
            };

            await catalog.PreparePreviewsAsync();
            catalog.OnScanProgress = null;
            _loadingOverlay.Hide();
        }
        await ShowSabers(false);

        if (previewSession?.FocusedSaber == null)
            editor?.ActivateEditor();
    }

    public void UpdateCellIcon(object userData, UnityEngine.Sprite icon) => _saberList?.UpdateCellIcon(userData, icon);

    public async Task ShowSabers(bool scrollToTop = false)
    {
        if (catalog is null || catalogController.Folders is null) return;

        await previewSession.EditorReady;

        _saberList.SetItems(BuildSortedListItems());

        var current = previewSession?.ActiveEntry;

        var loadoutEntry = editor?.LoadoutEntry;
        if (loadoutEntry is not null && (current is null || !ReferenceEquals(loadoutEntry, current)))
            current = loadoutEntry;

        if (editor?.IsLoadoutEmpty == true)
        {
            previewSession?.WipePreviews();
            trailPreviewer?.Destroy();
            _saberList.Deselect();
            _grabSaberCombo?.SetSelected([]);
            _transformSabersCombo?.SetSelected([]);

            transformController.State.ResetToDefaults();
            _activeSaberLength = 1f;
            _activeSaberOffset = 0f;
            previewController?.SetTitle("");
        }
        else if (current is not null)
        {
            await selectionController.SelectAsync(current);
            var meta = catalog.FindPreviewForEntry(current);
            if (meta is not null) _saberList.Select(meta.DisplayName, !scrollToTop);
            if (previewController.TitleLabel is not null && current is ISaberListEntry ci)
                previewController.SetTitle(ci.DisplayName.ToUpper());
        }
        else
        {
            trailPreviewer?.Destroy();
            _saberList.Deselect();
            _grabSaberCombo?.SetSelected([]);
            _transformSabersCombo?.SetSelected([]);
            transformController.State.ResetToDefaults();
            _activeSaberLength = 1f;
            _activeSaberOffset = 0f;
            previewController?.SetTitle("");
        }

        if (scrollToTop) _saberList.ScrollTo(0);
        Refresh();
    }

    private async Task SaberSelectedAsync(int _, UIListCellData data)
    {
        try
        {
            if (data.UserData is FolderEntry dir)
            {
                catalogController.Folders!.Navigate(dir.DisplayName);
                _saberList.Deselect();
                await ShowSabers(true);
                return;
            }

            if (!Plugin.MultiPassEnabled && data.UserData is AssetPreview preview && !preview.IsSPICompatible)
            {
                _messagePopup?.Show("This saber requires multi-pass\nrendering to be enabled.\n\nEnable it in Mod Settings \u2192 Asset Bundles.");
                _saberList.Deselect();
                return;
            }

            if (data.UserData is ISaberListEntry listItem)
            {
                previewController.SetTitle(listItem.DisplayName.ToUpper());
            }

            SaberAssetEntry? entry = null;
            bool showedOverlay = false;

            if (data.UserData is AssetPreview metaData)
            {
                if (catalog?.TryGetLoaded(metaData.RelativePath) is null && _loadingOverlay is not null)
                {
                    await _loadingOverlay.ShowAsync();
                    showedOverlay = true;
                }
                entry = await selectionController.ResolveAsync(metaData);
            }
            else if (data.UserData is SaberAssetEntry comp)
                entry = comp;
            else return;

            if (entry is null)
            {
                if (showedOverlay) _loadingOverlay?.Hide();
                return;
            }

            if (entry == selectionController.SelectedEntry)
            {
                if (showedOverlay) _loadingOverlay?.Hide();
                return;
            }

            if (!Plugin.MultiPassEnabled && !entry.IsSPICompatible)
            {
                if (showedOverlay) _loadingOverlay?.Hide();
                _messagePopup?.Show("This saber requires multi-pass\nrendering to be enabled.\n\nEnable it in Mod Settings \u2192 Asset Bundles.");
                _saberList.Deselect();
                return;
            }

            if (!showedOverlay && _loadingOverlay is not null)
                await _loadingOverlay.ShowAsync();

            _loadingOverlay?.SetPhase("Equipping saber...", 0.7f);

            try
            {
                _spawnComplete = new();
                await selectionController.SelectAsync(entry);
                _loadingOverlay?.SetPhase("Spawning preview...", 0.85f);
                await Task.WhenAny(_spawnComplete.Task, Task.Delay(10000));
            }
            finally
            {
                _loadingOverlay?.Hide();
            }
        }
        catch (Exception ex) { _log.Error($"SaberSelected failed: {ex}"); }
    }

    private async Task TogglePinAsync(bool isOn)
    {
        try
        {
            var entry = selectionController.SelectedEntry;
            if (entry is null) return;
            catalogController.SetPinned(entry, isOn);
            Refresh();
            await ShowSabers();
        }
        catch (Exception ex) { _log.Error($"ToggledFavorite failed: {ex}"); }
    }

    private void BuildRightColumn(RectTransform parent)
    {
        var rightCol = new VBox("RightCol").SetParent(parent).SetAlignment(TextAnchor.UpperLeft);
        UnityEngine.Object.Destroy(rightCol.GameObject.GetComponent<ContentSizeFitter>());
        rightCol.LayoutGroup.childForceExpandHeight = false;
        rightCol.SetPadding(0, 0, 0, 0).SetSpacing(UITheme.GroupGap)
            .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var optionsGroup = new UIGroupBox("Saber options");
        optionsGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _favoriteButton = new BaseButton("Pin saber").SetParent(optionsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        _favoriteButton.OnClick = () => ErrorBoundary.FireAndForget(TogglePinAsync(!(selectionController.SelectedEntry?.IsPinned ?? false)), _log, nameof(TogglePinAsync));

        var sortCombo = new UIComboBox("SortCombo", _canvasRoot);
        sortCombo.SetOptions([.. SaberSense.GUI.Framework.UIStrings.SortModes]);
        sortCombo.BindInt(pluginConfig, c => c.Editor.SortMode, async (idx) =>
        {
            catalogController.SortMode = (SaberCatalogController.ESortMode)idx;
            await ShowSabers(true);
        }, scope: _bindingScope);
        UILayoutFactory.DropdownRow("Sort by", sortCombo, optionsGroup.Content);

        _grabSaberCombo = new UIMultiComboBox("GrabSaberCombo", _canvasRoot);
        _grabSaberCombo.SetOptions([.. SaberSense.GUI.Framework.UIStrings.GrabOptions]);
        _grabSaberCombo.BindList(pluginConfig, c => c.GrabSelections, sel =>
        {
            if (editor is null) return;
            bool left = sel.Contains(0);
            bool right = sel.Contains(1);
            editor.SetGrab(left, right);
        }, scope: _bindingScope);
        UILayoutFactory.DropdownRow("Grab saber", _grabSaberCombo, optionsGroup.Content);
        if (editor is not null && pluginConfig?.GrabSelections is not null)
        {
            HashSet<int> initGrab = [.. pluginConfig.GrabSelections];
            editor.SetGrab(initGrab.Contains(0), initGrab.Contains(1));
        }

        BuildTransformControls(optionsGroup.Content);

        GameObject? goSmoothSlider = null;
        var smoothToggle = new UIToggle().Bind(pluginConfig!, c => c.SmoothingEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Motion smoothing", smoothToggle, optionsGroup.Content);

        var smoothSlider = new UISlider().SetRange(0, 100).Bind(pluginConfig!, c => c.SmoothingStrength, scope: _bindingScope);
        goSmoothSlider = UILayoutFactory.SliderRow("Smoothing strength", smoothSlider, optionsGroup.Content);
        smoothToggle.ControlsVisibility(goSmoothSlider);

        GameObject? goBlurSlider = null;
        var blurToggle = new UIToggle().Bind(pluginConfig!, c => c.MotionBlur.Enabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Motion blur", blurToggle, optionsGroup.Content, experimental: true);

        var blurSlider = new UISlider().SetRange(0, 100).Bind(pluginConfig!, c => c.MotionBlur.Strength, scope: _bindingScope);
        goBlurSlider = UILayoutFactory.SliderRow("Blur strength", blurSlider, optionsGroup.Content);
        blurToggle.ControlsVisibility(goBlurSlider);

        var previewToggle = new UIToggle().Bind(pluginConfig!, c => c.Editor.PreviewSaber, val =>
        {
            _previewWindowGO?.SetActive(val);
        }, scope: _bindingScope);
        _previewWindowGO?.SetActive(pluginConfig?.Editor?.PreviewSaber ?? true);
        UILayoutFactory.CheckboxRow("Preview saber", previewToggle, optionsGroup.Content);
    }

    private List<UIListCellData> BuildSortedListItems()
    {
        var metaEnumerable = System.Linq.Enumerable.OrderByDescending(
            catalog!.EnumeratePreviewsByTag(AssetTypeTag.SaberAsset), meta => meta.IsPinned);
        var sortMode = (SaberCatalogController.ESortMode)(pluginConfig?.Editor?.SortMode ?? 0);
        catalogController.SortMode = sortMode;
        switch (sortMode)
        {
            case SaberCatalogController.ESortMode.Name: metaEnumerable = System.Linq.Enumerable.ThenBy(metaEnumerable, x => x.DisplayName); break;
            case SaberCatalogController.ESortMode.Date: metaEnumerable = System.Linq.Enumerable.ThenByDescending(metaEnumerable, x => x.FileLastModifiedTicks); break;
            case SaberCatalogController.ESortMode.Size: metaEnumerable = System.Linq.Enumerable.ThenByDescending(metaEnumerable, x => x.FileSize); break;
            case SaberCatalogController.ESortMode.Author: metaEnumerable = System.Linq.Enumerable.ThenBy(metaEnumerable, x => x.CreatorName); break;
        }

        List<ISaberListEntry> items = [.. metaEnumerable];
        var processed = catalogController.Folders!.Process(items);

        var folderSprite = SaberSense.GUI.Framework.Core.VectorSpriteGenerator.Generate(
            SaberSense.GUI.Framework.Core.IconPaths.Folder, 64);
        var returnSprite = SaberSense.GUI.Framework.Core.VectorSpriteGenerator.Generate(
            SaberSense.GUI.Framework.Core.IconPaths.Return, 64);

        List<UIListCellData> uiItems = [];
        foreach (var item in processed)
        {
            if (item is FolderEntry dir)
            {
                var isUp = dir.DisplayName == "<";
                uiItems.Add(new UIListCellData(isUp ? "Back" : dir.DisplayName, isUp ? "Return to parent" : "Directory", isUp ? returnSprite : folderSprite, dir));
            }
            else if (!string.IsNullOrEmpty(item.DisplayName))
                uiItems.Add(new UIListCellData(item.DisplayName, item.CreatorName ?? "", item.CoverImage, item, item.IsPinned));
        }

        if (uiItems.Count is 0) uiItems.Add(new UIListCellData("No sabers found", ""));
        return uiItems;
    }

    private void BuildTransformControls(RectTransform parent)
    {
        HashSet<int> activeTransformSels = [];

        _transformSabersCombo = new UIMultiComboBox("TransformSabersCombo", _canvasRoot);
        _transformSabersCombo.SetOptions([.. SaberSense.GUI.Framework.UIStrings.TransformOptions]);
        _transformSabersCombo.BindList(pluginConfig!, c => c.TransformSelections, sel => { activeTransformSels = sel; }, scope: _bindingScope);
        UILayoutFactory.DropdownRow("Transform sabers", _transformSabersCombo, parent);

        _saberWidthSlider = new UISlider().SetRange(0, 3).Bind(transformController.State, t => t.Width, val =>
        {
            if (transformController.IsSyncing) return;
            transformController.SetWidth(selectionController.SelectedEntry!, val);
            previewController.SaberPreview?.RefreshFraming(_activeSaberLength, _activeSaberOffset);
        }, scope: _bindingScope);
        var goSaberWidth = UILayoutFactory.SliderRow("Saber width", _saberWidthSlider, parent);

        _saberLengthSlider = new UISlider().SetRange(0.1f, 3f).Bind(transformController.State, t => t.Length, val =>
        {
            if (transformController.IsSyncing) return;
            _activeSaberLength = val;
            transformController.SetLength(selectionController.SelectedEntry!, val);
            previewController.SaberPreview?.RefreshFraming(val, _activeSaberOffset);
            trailPreviewer?.UpdatePosition();
        }, scope: _bindingScope);
        var goSaberLength = UILayoutFactory.SliderRow("Saber length", _saberLengthSlider, parent);

        _rotationSlider = new UISlider().SetRange(-180, 180).Bind(transformController.State, t => t.Rotation, val =>
        {
            if (transformController.IsSyncing) return;
            transformController.SetRotation(selectionController.SelectedEntry!, val);
        }, scope: _bindingScope);
        var goRotation = UILayoutFactory.SliderRow("Rotation amount", _rotationSlider, parent);

        _offsetSlider = new UISlider().SetRange(-0.5f, 0.5f).Bind(transformController.State, t => t.Offset, val =>
        {
            if (transformController.IsSyncing) return;
            _activeSaberOffset = val;
            transformController.SetOffset(selectionController.SelectedEntry!, val);
            previewController.SaberPreview?.RefreshFraming(_activeSaberLength, val);
            trailPreviewer?.UpdatePosition();
        }, scope: _bindingScope);
        var goOffset = UILayoutFactory.SliderRow("Offset amount", _offsetSlider, parent);

        var btnRevertTransform = new BaseButton("Revert").SetParent(parent).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        btnRevertTransform.OnClick = () =>
        {
            var entry = selectionController.SelectedEntry;
            if (activeTransformSels.Contains(0)) transformController.ResetWidth(entry!);
            if (activeTransformSels.Contains(1)) transformController.ResetLength(entry!);
            if (activeTransformSels.Contains(2)) transformController.ResetRotation(entry!);
            if (activeTransformSels.Contains(3)) transformController.ResetOffset(entry!);
            transformController.SyncFromActiveSaber();
            _activeSaberLength = activeTransformSels.Contains(1) ? 1f : _activeSaberLength;
            _activeSaberOffset = activeTransformSels.Contains(3) ? 0f : _activeSaberOffset;
            broker?.Publish(new SaberSense.Core.Messaging.PreviewSaberChangedMsg(entry!));
        };

        _transformSabersCombo.ControlsVisibility(0, goSaberWidth);
        _transformSabersCombo.ControlsVisibility(1, goSaberLength);
        _transformSabersCombo.ControlsVisibility(2, goRotation);
        _transformSabersCombo.ControlsVisibility(3, goOffset);
        _transformSabersCombo.ShowWhenAnySelected(btnRevertTransform.GameObject);
    }

    private void OnLoadProgress(string phase, float progress)
    {
        if (_loadingOverlay is not null && !_loadingOverlay.IsVisible && !_externalLoadOverlayActive)
        {
            _externalLoadOverlayActive = true;
            ErrorBoundary.FireAndForget(_loadingOverlay.ShowAsync(), _log, "AutoLoadOverlay");
        }
        _loadingOverlay?.SetPhase(phase, progress);
    }
    private void OnAssetProgress(int loaded, int total) =>
        _loadingOverlay?.SetPhase($"Loading assets [{loaded}/{total}]", 0.15f + (0.25f * loaded / total));

    public void Dispose()
    {
        SaberSense.Loaders.SaberBundleLoader.OnLoadProgress -= OnLoadProgress;
        SaberSense.Core.Utilities.BundleLoader.OnAssetProgress -= OnAssetProgress;
        _bindingScope.Dispose();
        _equippedSub?.Dispose();
        _spawnedSub?.Dispose();
        _settingsChangedSub?.Dispose();
        _configLoadingSub?.Dispose();
        _configLoadedSub?.Dispose();
        _loadingOverlay?.Dispose();
    }
}