// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Services;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Menu.Tabs;

internal sealed class SettingsTabView : IDisposable
{
    private readonly ModSettings _pluginConfig;
    private readonly InternalConfig _internalConfig;
    private readonly AppPaths _appPaths;
    private readonly IMessageBroker _broker;
    private readonly DefaultSaberProvider _defaultSaberProvider;
    private readonly ConfigManager _configManager;
    private readonly TrailSettingsController _trailController;
    private readonly SaberTransformController _transformController;
    private readonly PreviewSession _previewSession;
    private readonly SaberLoadout _saberSet;
    private readonly IModLogger _log;

    private RectTransform _canvasRoot = null!;
    private readonly BindingScope _bindingScope = new();

    private GameObject _infoContent = null!;
    private GameObject _gameplayGroup = null!;
    private GameObject _renderingGroup = null!;

    private WorldModSection _worldModSection = null!;
    private ConfigManagementSection _configSection = null!;

    public SettingsTabView(
        ModSettings config,
        InternalConfig internalConfig,
        AppPaths appPaths,
        IMessageBroker broker,
        DefaultSaberProvider defaultSaberProvider,
        ConfigManager configManager,
        TrailSettingsController trailController,
        SaberTransformController transformController,
        PreviewSession previewSession,
        SaberLoadout saberSet,
        IModLogger log)
    {
        _pluginConfig = config;
        _internalConfig = internalConfig;
        _appPaths = appPaths;
        _broker = broker;
        _defaultSaberProvider = defaultSaberProvider;
        _configManager = configManager;
        _trailController = trailController;
        _transformController = transformController;
        _previewSession = previewSession;
        _saberSet = saberSet;
        _log = log.ForSource(nameof(SettingsTabView));
    }

    public GameObject Build(RectTransform parent, RectTransform canvasRoot)
    {
        _canvasRoot = canvasRoot;
        _worldModSection = new WorldModSection(_pluginConfig, _bindingScope, canvasRoot);
        _configSection = new ConfigManagementSection(
            _pluginConfig, _internalConfig, _appPaths, _broker,
            _configManager, _trailController, _transformController,
            _defaultSaberProvider, canvasRoot, _log);

        var root = UILayoutFactory.TabRoot("SettingsTab", parent);

        var columns = new HBox("SettingsCols").SetParent(root);
        UnityEngine.Object.Destroy(columns.GameObject.GetComponent<ContentSizeFitter>());
        columns.SetSpacing(UITheme.ColumnGap).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        BuildLeftColumn(columns.RectTransform);
        BuildRightColumn(columns.RectTransform);

        _configSection.SetPanelCallbacks(
            hideNormalPanels: () =>
            {
                _infoContent?.SetActive(false);
                _gameplayGroup?.SetActive(false);
                _renderingGroup?.SetActive(false);
            },
            showNormalPanels: () =>
            {
                _infoContent?.SetActive(true);
                _gameplayGroup?.SetActive(true);
                _renderingGroup?.SetActive(true);
            });

        return root.gameObject;
    }

    private void BuildLeftColumn(RectTransform parent)
    {
        var leftCol = new VBox("LeftCol").SetParent(parent).SetAlignment(TextAnchor.UpperLeft);
        UnityEngine.Object.Destroy(leftCol.GameObject.GetComponent<ContentSizeFitter>());
        leftCol.SetPadding(0, 0, 0, 0).SetSpacing(UITheme.GroupGap).AddLayoutElement(flexibleWidth: 1);

        var leftGroup = new UIGroupBox("Gameplay");
        leftGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _gameplayGroup = leftGroup.GameObject;

        BuildGameplaySection(leftGroup.Content);
        BuildRenderingSection(leftCol.RectTransform);
        _worldModSection.Build(leftGroup.Content);

        _configSection.BuildControlsPanel(leftCol.RectTransform);
    }

    private void BuildGameplaySection(RectTransform parent)
    {
        var enabledToggle = new UIToggle().Bind(_pluginConfig, c => c.IsActive, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Enable SaberSense", enabledToggle, parent);

        var eventManagerToggle = new UIToggle().Bind(_pluginConfig, c => c.EnableEventManager, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Event manager", eventManagerToggle, parent);

        var randomToggle = new UIToggle().Bind(_pluginConfig, c => c.RandomizeSaber, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Randomize saber", randomToggle, parent);

        BuildDefaultSaberToggle(parent);
        BuildPauseKeyRow(parent);
        BuildWarningMarkerRows(parent);
        BuildHidePlatformRow(parent);
        BuildKeepSabersOnFocusLossRow(parent);
        BuildFloorCalibrationRows(parent);
    }

    private void BuildDefaultSaberToggle(RectTransform parent)
    {
        var defaultSaberToggle = new UIToggle().Bind(_pluginConfig, c => c.ShowDefaultSaber, val =>
        {
            if (val)
            {
                _defaultSaberProvider.Register();
            }
            else
            {
                if (IsDefaultSaberEquipped())
                {
                    _previewSession?.WipePreviews();
                    _saberSet.Left.Pieces.Clear();
                    _saberSet.Right.Pieces.Clear();
                }

                ClearDefaultSaberTrailReferences();
                _defaultSaberProvider.Unregister();
            }
            _broker?.Publish(new SettingsChangedMsg());
        }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Show default saber", defaultSaberToggle, parent);

        if (_pluginConfig.ShowDefaultSaber) _defaultSaberProvider?.Register();
        else _defaultSaberProvider?.Unregister();
    }

    private void BuildPauseKeyRow(RectTransform parent)
    {
        var pauseKeybind = new UIKeybindButton("PauseKeybind");
        pauseKeybind.BindInt(_pluginConfig, c => c.PauseKeyButton, idx =>
        {
            SaberSense.Core.PauseKeyInputBehavior.Binding = idx;
        }, scope: _bindingScope);
        var pauseToggle = new UIToggle().Bind(_pluginConfig, c => c.PauseKeyEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxKeybindRow("Override pause key", pauseToggle, pauseKeybind, parent);
    }

    private void BuildWarningMarkerRows(RectTransform parent)
    {
        var warningToggle = new UIToggle().Bind(_pluginConfig, c => c.WarningMarkerEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Warning markers", warningToggle, parent);

        var warningTypesCombo = new UIMultiComboBox("WarningTypesCombo", _canvasRoot);
        warningTypesCombo.SetOptions(["Resets", "Horizontals", "All notes"]);
        warningTypesCombo.BindList(_pluginConfig, c => c.WarningTypes, scope: _bindingScope);
        var goWarningTypes = UILayoutFactory.DropdownRow("Warning types", warningTypesCombo, parent);
        warningToggle.ControlsVisibility(goWarningTypes);

        var warningLayerCombo = new UIMultiComboBox("WarningLayerCombo", _canvasRoot);
        warningLayerCombo.SetOptions(["Top", "Middle", "Bottom"]);
        warningLayerCombo.BindList(_pluginConfig, c => c.WarningLayerFilter, scope: _bindingScope);
        var goWarningLayers = UILayoutFactory.DropdownRow("Layer filter", warningLayerCombo, parent);
        warningToggle.ControlsVisibility(goWarningLayers);
    }

    private void BuildHidePlatformRow(RectTransform parent)
    {
        var hidePlatformToggle = new UIToggle().Bind(_pluginConfig, c => c.HidePlatform, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Hide platform", hidePlatformToggle, parent);
    }

    private void BuildKeepSabersOnFocusLossRow(RectTransform parent)
    {
        var toggle = new UIToggle().Bind(_pluginConfig, c => c.KeepSabersOnFocusLoss, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Disable saber standby", toggle, parent);
    }

    private void BuildFloorCalibrationRows(RectTransform parent)
    {
        var toggle = new UIToggle().Bind(_pluginConfig, c => c.FloorCalibrationEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Floor calibration", toggle, parent);

        var calibrateBtn = new BaseButton("Calibrate floor").SetParent(parent)
            .AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        calibrateBtn.OnClick = OnCalibrateFloor;
        toggle.ControlsVisibility(calibrateBtn.GameObject);
    }

    private void BuildRenderingSection(RectTransform parentCol)
    {
        var renderingGroup = new UIGroupBox("Rendering");
        renderingGroup.SetParent(parentCol).AddLayoutElement(flexibleWidth: 1, preferredHeight: 26);
        _renderingGroup = renderingGroup.GameObject;

        var desktopVisCombo = new UIMultiComboBox("DesktopVisCombo", _canvasRoot);
        desktopVisCombo.SetOptions(SaberSense.Core.ViewFeatureRegistry.GetAllLabels());
        if (_pluginConfig is not null) desktopVisCombo.BindList(_pluginConfig, c => c.Visibility.Desktop, scope: _bindingScope);
        UILayoutFactory.DropdownRow("Desktop view", desktopVisCombo, renderingGroup.Content);

        var hmdVisCombo = new UIMultiComboBox("HmdVisCombo", _canvasRoot);
        hmdVisCombo.SetOptions(SaberSense.Core.ViewFeatureRegistry.GetAllLabels());
        if (_pluginConfig is not null) hmdVisCombo.BindList(_pluginConfig, c => c.Visibility.Hmd, scope: _bindingScope);
        UILayoutFactory.DropdownRow("HMD view", hmdVisCombo, renderingGroup.Content);
    }

    private void OnCalibrateFloor()
    {
        var left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        var right = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);

        bool hasLeft = left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var lp);
        bool hasRight = right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var rp);

        if (!hasLeft && !hasRight) return;

        float floorY;
        if (hasLeft && hasRight) floorY = Mathf.Min(lp.y, rp.y);
        else floorY = hasLeft ? lp.y : rp.y;

        const float controllerThickness = 0.035f;
        float calibration = -(floorY - controllerThickness);

        if (_pluginConfig is not null)
        {
            _pluginConfig.FloorCalibrationY = calibration;
            SaberSense.Core.Patches.FloorCalibrationPatch.ApplyCalibration(calibration);
        }
    }

    private void BuildRightColumn(RectTransform parent)
    {
        var rightCol = new VBox("RightCol").SetParent(parent);
        UnityEngine.Object.Destroy(rightCol.GameObject.GetComponent<ContentSizeFitter>());
        rightCol.SetSpacing(UITheme.GroupGap).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var infoGroup = new UIGroupBox("Information");
        infoGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var actionKeybind = new UIKeybindButton("ActionKeybind");
        actionKeybind.BindInt(_pluginConfig, c => c.ActionKeyButton, idx =>
        {
            SaberSense.Core.ActionKeyInputBehavior.Binding = idx;
        }, scope: _bindingScope);
        UILayoutFactory.KeybindRow("Action key", actionKeybind, infoGroup.Content);
        SaberSense.Core.ActionKeyInputBehavior.Binding = _pluginConfig?.ActionKeyButton ?? 0;

        var accentRow = new HBox("AccentColorRow").SetParent(infoGroup.Content);
        accentRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        accentRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        accentRow.LayoutGroup.childForceExpandHeight = false;
        new UILabel("AccentL", "Accent color").SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
            .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(accentRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        var accentPicker = new UIColorPicker("AccentCP", _canvasRoot)
            .SetColor(_pluginConfig?.AccentColor ?? UITheme.Accent)
            .OnColorChanged(c =>
            {
                UITheme.SetAccentLive(c);
            })
            .OnCommit(c =>
            {
                UITheme.SetAccent(c);
                if (_pluginConfig is not null) _pluginConfig.AccentColor = c;
            });
        accentPicker.SetParent(accentRow.RectTransform).AddLayoutElement(preferredWidth: UITheme.SwatchWidth, preferredHeight: UITheme.SwatchHeight);

        if (_pluginConfig is not null)
        {
            _bindingScope.Add(_pluginConfig!, (_, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName.StartsWith("Accent"))
                    accentPicker.SetColor(_pluginConfig.AccentColor);
            });
        }
        accentPicker.SetResetColor(new Color(0.62f, 0.79f, 0.16f, 1f));
        var discordBtn = new BaseButton("Discord").SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        discordBtn.OnClick = () => OpenExternal(SaberSense.Core.ExternalLinks.Discord);

        new UILabel("Sp2", "").SetParent(infoGroup.Content).AddLayoutElement(flexibleHeight: 1);

        var getMoreBtn = new BaseButton("Get more sabers").SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        getMoreBtn.OnClick = () => OpenExternal(SaberSense.Core.ExternalLinks.ModelSaber);

        new UILabel("Sp6", "").SetParent(infoGroup.Content).AddLayoutElement(flexibleHeight: 1);

        var configBtn = new BaseButton("Configuration").SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        configBtn.OnClick = () => _configSection.Show();

        new UILabel("Sp5b", "").SetParent(infoGroup.Content).AddLayoutElement(flexibleHeight: 1);

        new UILabel("Credit", "mod by youtube.com/dylanhook")
            .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextVersion)
            .SetAlignment(TMPro.TextAlignmentOptions.Center)
            .SetParent(infoGroup.Content).AddLayoutElement(preferredHeight: UITheme.SectionLabelHeight);

        _infoContent = infoGroup.GameObject;

        _configSection.BuildConfigPanel(rightCol.RectTransform);
    }

    private void OpenExternal(string path)
    {
        try { System.Diagnostics.Process.Start(path); }
        catch (Exception ex) { _log.Debug($"Open external failed: {ex.Message}"); }
    }

    public void Dispose()
    {
        _bindingScope.Dispose();
        _configSection.Dispose();
    }

    private bool IsDefaultSaberEquipped()
    {
        var entry = _previewSession?.ActiveEntry;
        if (entry is null) return false;
        return entry.LeftPiece?.Asset?.RelativePath == DefaultSaberProvider.DefaultSaberPath;
    }

    private void ClearDefaultSaberTrailReferences()
    {
        bool cleared = false;
        cleared |= ClearTrailIfDefault(_saberSet.Left);
        cleared |= ClearTrailIfDefault(_saberSet.Right);

        if (cleared)
            _broker?.Publish(new SaberSense.Core.Messaging.PreviewSaberChangedMsg(_previewSession?.ActiveEntry!));

        static bool ClearTrailIfDefault(SaberProfile profile)
        {
            if (profile?.Snapshot?.TrailSettings is { } ts
                && ts.OriginAssetPath == DefaultSaberProvider.DefaultSaberPath)
            {
                profile.Snapshot.TrailSettings = null;
                profile.NotifyChanged();
                return true;
            }
            return false;
        }
    }
}