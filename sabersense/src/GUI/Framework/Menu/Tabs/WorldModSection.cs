// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.GUI.Framework.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.GUI.Framework.Menu.Tabs;

internal sealed class WorldModSection
{
    private readonly ModSettings _config;
    private readonly BindingScope _scope;
    private readonly RectTransform _canvasRoot;

    private Dictionary<int, VisibilityGroup>? _colorRowGroups;
    private Dictionary<int, UIColorPicker>? _colorPickers;
    private UIColorPicker? _inlinePicker;
    private UIMultiComboBox? _modes;
    private VisibilityGroup? _overrideVg;

    public WorldModSection(ModSettings config, BindingScope scope, RectTransform canvasRoot)
    {
        _config = config;
        _scope = scope;
        _canvasRoot = canvasRoot;
    }

    public void Build(RectTransform parent)
    {
        _colorRowGroups = null;
        _inlinePicker = null;
        _modes = null;
        _overrideVg = null;

        var worldModToggle = new UIToggle().Bind(_config, c => c.WorldMod.Enabled, on =>
        {
            if (_colorRowGroups is not null)
                foreach (var vg in _colorRowGroups.Values)
                    vg.SetCondition("worldMod", on);
            _overrideVg?.SetCondition("worldMod", on);
            if (_inlinePicker is not null)
            {
                if (!on) _inlinePicker.GameObject.SetActive(false);
                else if (_modes is not null) SyncInlinePicker(_modes.SelectedIndices);
            }
        }, scope: _scope);
        UILayoutFactory.CheckboxRow("World modulation", worldModToggle, parent);

        _modes = new UIMultiComboBox("WorldModModes", _canvasRoot);
        _modes.SetOptions(SaberSense.Core.WorldModulationModeRegistry.GetAllLabels());
        _modes.BindList(_config, c => c.WorldMod.Modes, scope: _scope);
        var modesRow = new VBox("OptionsRow").SetParent(parent);
        modesRow.SetSpacing(UITheme.RowInnerSpacing).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.DropdownRowHeight, flexibleWidth: 1);
        modesRow.LayoutGroup.childForceExpandWidth = true;
        new UILabel("OptionsL", "Options").SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
            .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(modesRow.RectTransform).AddLayoutElement(preferredHeight: UITheme.LabelHeight);
        _modes.SetParent(modesRow.RectTransform).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        worldModToggle.ControlsVisibility(modesRow.GameObject);

        var worldModStrength = new UISlider().SetRange(0, 100).Bind(_config, c => c.WorldMod.Strength, scope: _scope);
        var strengthRow = UILayoutFactory.SliderRow("Strength", worldModStrength, parent);
        worldModToggle.ControlsVisibility(strengthRow);

        BuildColorOverride(parent, worldModToggle);

        BuildPerModeColorPickers(parent);

        WireModeSelection();

        SyncInlinePicker(_modes.SelectedIndices);

        _scope.Add(_config, (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "WorldMod")
                SyncInlinePicker(_modes.SelectedIndices);
        });
    }

    private void BuildColorOverride(RectTransform parent, UIToggle worldModToggle)
    {
        var overrideToggle = new UIToggle().Bind(_config, c => c.WorldMod.OverrideColor, on =>
        {
            if (_colorRowGroups is not null)
                foreach (var vg in _colorRowGroups.Values)
                    vg.SetCondition("override", on);
            if (_modes is not null) SyncInlinePicker(_modes.SelectedIndices);
        }, scope: _scope);
        var overrideRow = new HBox("ColorOverrideRow").SetParent(parent);
        overrideRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        overrideRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        overrideRow.LayoutGroup.childForceExpandHeight = false;
        overrideToggle.SetParent(overrideRow.RectTransform);
        new UILabel("ColorOverrideL", "Color override").SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
            .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(overrideRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        UILayoutFactory.AddRowHitArea(overrideRow.RectTransform, overrideToggle);

        _overrideVg = new VisibilityGroup(overrideRow.GameObject);
        _overrideVg.SetCondition("worldMod", _config.WorldMod?.Enabled ?? false);
        _overrideVg.SetCondition("overrideOption", _modes!.SelectedIndices.Contains(WorldModulationOptions.OverrideColor));

        _inlinePicker = new UIColorPicker("InlineCP", _canvasRoot);
        _inlinePicker.SetParent(overrideRow.RectTransform).AddLayoutElement(preferredWidth: UITheme.SwatchWidth, preferredHeight: UITheme.SwatchHeight);
        _inlinePicker.GameObject.SetActive(false);
    }

    private void BuildPerModeColorPickers(RectTransform parent)
    {
        var allModes = (WorldModulationMode[])Enum.GetValues(typeof(WorldModulationMode));
        _colorRowGroups = [];
        _colorPickers = [];

        foreach (var mode in allModes)
        {
            int idx = (int)mode;
            string label = WorldModulationModeRegistry.GetLabel(mode);

            var colorRow = new HBox($"{label}ColorRow").SetParent(parent);
            colorRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
            colorRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            colorRow.LayoutGroup.childForceExpandHeight = false;
            new UILabel($"{label}CL", $"  {label} color").SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
                .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(colorRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);

            var capturedMode = mode;
            var cp = new UIColorPicker($"{label}CP", _canvasRoot)
                .BindColor(_config!,
                    () => _config?.WorldMod?.GetColorForMode(capturedMode) ?? Color.white,
                    c => _config?.WorldMod?.SetColorForMode(capturedMode, c),
                    "WorldMod",
                    scope: _scope);
            cp.SetParent(colorRow.RectTransform).AddLayoutElement(preferredWidth: UITheme.SwatchWidth, preferredHeight: UITheme.SwatchHeight);
            _colorPickers[idx] = cp;

            var vg = new VisibilityGroup(colorRow.GameObject);
            vg.SetCondition("worldMod", _config?.WorldMod?.Enabled ?? false);
            vg.SetCondition("override", _config?.WorldMod?.OverrideColor ?? false);
            vg.SetCondition("modeSelected", _modes!.SelectedIndices.Contains(idx));
            vg.SetCondition("multiMode", _modes!.SelectedIndices.Count is > 1);
            _colorRowGroups[idx] = vg;
        }
    }

    private void WireModeSelection()
    {
        var allModes = (WorldModulationMode[])Enum.GetValues(typeof(WorldModulationMode));

        _modes!.OnSelectionChanged(selected =>
        {
            bool overrideSelected = selected.Contains(WorldModulationOptions.OverrideColor);
            if (_config.WorldMod.OverrideColor != overrideSelected)
                _config.WorldMod.OverrideColor = overrideSelected;
            _overrideVg?.SetCondition("overrideOption", overrideSelected);

            var actualModes = selected.Where(i => i < 3).ToHashSet();
            bool isMulti = actualModes.Count is > 1;
            foreach (var mode in allModes)
            {
                int idx = (int)mode;
                if (_colorRowGroups!.TryGetValue(idx, out var vg))
                {
                    vg.SetCondition("modeSelected", actualModes.Contains(idx));
                    vg.SetCondition("multiMode", isMulti);
                }

                if (_colorPickers!.TryGetValue(idx, out var picker))
                    picker.SetColor(_config?.WorldMod?.GetColorForMode(mode) ?? Color.white);
            }
            SyncInlinePicker(selected);
        });
    }

    private void SyncInlinePicker(IReadOnlyCollection<int> selected)
    {
        bool overrideOn = _config?.WorldMod?.OverrideColor ?? false;
        var actualModes = selected.Where(i => i < 3).ToList();
        if (actualModes.Count is 1 && overrideOn)
        {
            int singleIdx = actualModes.First();
            var mode = (WorldModulationMode)singleIdx;
            _inlinePicker!.SetColor(_config?.WorldMod?.GetColorForMode(mode) ?? Color.white);
            _inlinePicker!.OnCommit(c =>
            {
                if (_config is not null)
                {
                    _config.WorldMod.SetColorForMode(mode, c);
                    _config.RaisePropertyChanged("WorldMod.Color");
                }
            });
            _inlinePicker.GameObject.SetActive(true);
        }
        else
        {
            _inlinePicker!.GameObject.SetActive(false);
        }
    }
}