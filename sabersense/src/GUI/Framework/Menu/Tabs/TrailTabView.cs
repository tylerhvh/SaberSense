// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Framework.Menu.Tabs;

internal sealed class TrailTabView(
    SaberSelectionController selectionController,
    ModSettings pluginConfig,
    PreviewSession previewSession,
    TrailSettingsController trailController,
    SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
    SaberSense.Catalog.SaberCatalog catalog,
    Services.SaberCatalogService catalogService,
    IMessageBroker broker) : IDisposable
{
    private readonly BindingScope _bindingScope = new();

    private UISlider _lenSlider = null!;
    private UISlider _widSlider = null!;
    private UISlider _wstepSlider = null!;
    private UISlider _offSlider = null!;
    private UISlider _granSlider = null!;
    private UISlider _freqSlider = null!;
    private UIToggle _flipToggle = null!;
    private UIToggle _clampToggle = null!;

    private RectTransform _canvasRoot = null!;
    private NativeMaterialEditor _materialEditor = null!;
    private NativeChooseTrailPopup _chooseTrailPopup = null!;

    public GameObject Build(RectTransform parent, RectTransform canvasRoot,
        NativeMaterialEditor materialEditor, NativeChooseTrailPopup chooseTrailPopup)
    {
        _canvasRoot = canvasRoot;
        _materialEditor = materialEditor;
        _chooseTrailPopup = chooseTrailPopup;

        var root = UILayoutFactory.TabRoot("TrailTab", parent);

        var (columns, leftCol, rightCol) = UILayoutFactory.TabColumns(root);

        var advGroup = new UIGroupBox("Advanced");
        advGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: 22);
        _granSlider = new UISlider().SetRange(0, 100).BindInt(pluginConfig, c => c.Trail.CurveSmoothnessPercent, _ => { broker?.Publish(new TrailSettingsChangedMsg()); }, scope: _bindingScope);
        UILayoutFactory.SliderRow("Curve smoothness", _granSlider, advGroup.Content);
        _freqSlider = new UISlider().SetRange(0, 144).BindInt(pluginConfig, c => c.Trail.CaptureSamplesPerSecond, _ => { broker?.Publish(new TrailSettingsChangedMsg()); }, scope: _bindingScope);
        _freqSlider.SetLabelFormatter(v => Mathf.RoundToInt(v) == 0 ? "Auto" : Mathf.RoundToInt(v).ToString());
        UILayoutFactory.SliderRow("Trail refresh rate", _freqSlider, advGroup.Content);

        var optionsGroup = new UIGroupBox("Rendering");
        optionsGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _flipToggle = new UIToggle().Bind(trailController.State, t => t.Flip, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetFlip(val);
        }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Flip trail", _flipToggle, optionsGroup.Content);
        _clampToggle = new UIToggle().Bind(trailController.State, t => t.ClampTexture,
            val => { if (!trailController.IsSyncing) trailController.SetClampTexture(val); }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Clamp texture", _clampToggle, optionsGroup.Content);
        var localSpaceToggle = new UIToggle().Bind(pluginConfig, c => c.Trail.LocalSpaceTrails, _ => { broker?.Publish(new TrailSettingsChangedMsg()); }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Local space trails", localSpaceToggle, optionsGroup.Content);
        var vertexToggle = new UIToggle().Bind(pluginConfig, c => c.Trail.VertexColorOnly, val => { if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = val; broker?.Publish(new TrailSettingsChangedMsg()); }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Use vertex color only", vertexToggle, optionsGroup.Content);
        if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = pluginConfig.Trail.VertexColorOnly;
        var trailSortToggle = new UIToggle().Bind(pluginConfig, c => c.Trail.OverrideTrailSortOrder, _ => { broker?.Publish(new TrailSettingsChangedMsg()); }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Override trail sort order", trailSortToggle, optionsGroup.Content, experimental: true);

        var primaryGroup = new UIGroupBox("Dimensions");
        primaryGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _lenSlider = new UISlider().SetRange(0, 100).Bind(trailController.State, t => t.LengthPercent, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetLength(val);
            trailPreviewer?.SetLength(previewSession?.FocusedSaber?.GetTrailLayout().Primary?.TrailSettings?.TrailLength ?? 14);
        }, scope: _bindingScope);
        _lenSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");
        UILayoutFactory.SliderRow("Trail duration", _lenSlider, primaryGroup.Content);
        _widSlider = new UISlider().SetRange(0, 100).Bind(trailController.State, t => t.WidthPercent, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetWidth(val);
            trailPreviewer?.UpdateWidth();
        }, scope: _bindingScope);
        _widSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");
        UILayoutFactory.SliderRow("Trail width", _widSlider, primaryGroup.Content);
        _wstepSlider = new UISlider().SetRange(0, 1).Bind(trailController.State, t => t.Whitestep,
            val => { if (!trailController.IsSyncing) trailController.SetWhitestep(val); }, scope: _bindingScope);
        UILayoutFactory.SliderRow("Whitestep", _wstepSlider, primaryGroup.Content);
        _offSlider = new UISlider().SetRange(-100, 100).Bind(trailController.State, t => t.OffsetPercent, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetOffset(val);
            trailPreviewer?.UpdateWidth();
        }, scope: _bindingScope);
        _offSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");
        UILayoutFactory.SliderRow("Offset", _offSlider, primaryGroup.Content);

        BuildActionRow(root);
        BindEvents();

        return root.gameObject;
    }

    public void Refresh() => trailController.SyncFromActiveSaber();

    private IDisposable? _selectionChangedSub;
    private IDisposable? _settingsChangedSub;

    private void BindEvents()
    {
        _selectionChangedSub = broker?.Subscribe<SaberEquippedMsg>(_ => Refresh())!;
        _settingsChangedSub = broker?.Subscribe<SettingsChangedMsg>(_ =>
        {
            Refresh();

            if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = pluginConfig.Trail.VertexColorOnly;
        })!;
    }

    private void BuildActionRow(RectTransform parent)
    {
        var actionRow = new HBox("TrailActions").SetParent(parent);
        actionRow.SetSpacing(UITheme.ColumnGap).AddLayoutElement(minHeight: UITheme.ActionRowHeight, preferredHeight: UITheme.ActionRowHeight, flexibleHeight: 0);
        var revertBtn = new BaseButton("Revert").SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        revertBtn.OnClick = () =>
        {
            if (pluginConfig is not null)
            {
                pluginConfig.Trail.CurveSmoothnessPercent = 60;
                pluginConfig.Trail.CaptureSamplesPerSecond = 0;
                pluginConfig.Trail.VertexColorOnly = true;
                pluginConfig.Trail.OverrideTrailSortOrder = false;
                pluginConfig.Trail.LocalSpaceTrails = false;
                _granSlider?.SetValue(60);
                _freqSlider?.SetValue(0);
                if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = true;

                pluginConfig.RaisePropertyChanged("Trail");
            }

            trailController.Revert(selectionController.SelectedEntry!);
        };
        var editMatBtn = new BaseButton("Edit material").SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        editMatBtn.OnClick = () =>
        {
            var td = previewSession?.FocusedSaber?.GetTrailLayout().Primary;
            SaberSense.Rendering.MaterialHandle? matDesc = null;
            if (td is not null) matDesc = td.Material;
            else matDesc = previewSession?.FocusedSaber?.Profile?.Snapshot?.TrailSettings?.Material;

            if (matDesc is not null && matDesc.Material != null && _materialEditor is not null)
            {
                _materialEditor.Show(matDesc);
            }
            else
            {
                var msg = new UIModal("Notice", _canvasRoot, 70, 30);
                var label = new UILabel("Msg", "No editable material is available for the currently selected trail.")
                    .SetFontSize(UITheme.FontSmall)
                    .SetColor(UITheme.TextPrimary)
                    .SetAlignment(TMPro.TextAlignmentOptions.Center);
                label.RectTransform.SetParent(msg.ContentArea.RectTransform, false);
                label.AddLayoutElement(flexibleHeight: 1);
                msg.AddButtons("OK", () => msg.Hide());
                msg.Show();
            }
        };

        var chooseTrailBtn = new BaseButton("Choose trail").SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        chooseTrailBtn.OnClick = () =>
        {
            if (_chooseTrailPopup is not null && catalog is not null)
            {
                var sortMode = (Controllers.SaberCatalogController.ESortMode)(pluginConfig?.Editor?.SortMode ?? 0);
                var currentOriginPath = previewSession?.FocusedSaber?.Profile?.Snapshot?.TrailSettings?.OriginAssetPath;
                _chooseTrailPopup.Show(
                    catalog.EnumeratePreviewsByTag(AssetTypeTag.SaberAsset),
                    sortMode,
                    catalog.ExternalSearchPaths,
                    TrailPopupSelectionChanged,
                    currentOriginPath
                );
            }
        };
    }

    private void TrailPopupSelectionChanged(TrailSettings? trailModel, List<SaberSense.Rendering.SaberTrailMarker>? trailList)
    {
        catalogService.ApplyTrailSelection(selectionController.SelectedEntry!, trailModel, trailList!, previewSession?.FocusedSaber);
        Refresh();
    }

    public void Dispose()
    {
        _bindingScope.Dispose();
        _selectionChangedSub?.Dispose();
        _settingsChangedSub?.Dispose();
    }
}