// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Framework.Menu.Builders;

internal sealed class MaterialPropertyRowBuilder(
    MaterialEditingController materialController,
    SplitPopupManager splitPopup,
    ColorPropertyBuilder colorBuilder,
    FloatPropertyBuilder floatBuilder,
    TogglePropertyBuilder toggleBuilder,
    TexturePropertyBuilder textureBuilder,
    PreviewSession previewSession)
{
    public List<GameObject> BuildPropertyRows(Material material, string matName,
        ConfigSnapshot snapshot, IReadOnlyList<ShaderProperty> shaderInfo,
        RectTransform parent, RectTransform canvasRoot)
    {
        List<GameObject> rows = [];

        var sourceHand = previewSession?.FocusedHand ?? SaberHand.Left;
        floatBuilder.CaptureSourceHand();
        colorBuilder.CaptureSourceHand();
        toggleBuilder.CaptureSourceHand();
        textureBuilder.CaptureSourceHand();

        bool hasSavedOverride = snapshot?.MaterialOverrides?.ContainsKey(matName) == true;
        float rawCustomColorsVal;
        if (snapshot?.MaterialOverrides?.TryGetValue(matName, out var _mo) == true && _mo["_CustomColors"] is not null)
            rawCustomColorsVal = _mo["_CustomColors"]!.ToObject<float>();
        else if (material.HasProperty(SaberSense.Core.Utilities.ShaderUtils.CustomColorToggleId))
            rawCustomColorsVal = materialController.GetOriginalFloat(matName, "_CustomColors");
        else
            rawCustomColorsVal = 0f;
        bool rawCustomColors = rawCustomColorsVal > 0.5f;
        bool overrideColorOn = hasSavedOverride && !rawCustomColors;

        List<GameObject> colorRows = [];

        ShaderProperty? customColorsProp = null;
        foreach (var p in shaderInfo)
        {
            if (p.Name == "_CustomColors") { customColorsProp = p; break; }
        }

        if (customColorsProp is not null)
        {
            var overrideRow = BuildOverrideColorRow(material, matName, snapshot!, customColorsProp,
                overrideColorOn, colorRows, parent, canvasRoot, sourceHand);
            rows.Add(overrideRow);

            if (material.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId)
                && snapshot is not null && snapshot.IsPropertySplit(matName, "_Color"))
            {
                var splitRows = colorBuilder.BuildSplitRows(material, matName,
                    FindProperty(shaderInfo, "_Color") ?? customColorsProp, snapshot, parent, canvasRoot);
                foreach (var row in splitRows)
                {
                    colorRows.Add(row);
                    row.SetActive(overrideColorOn);
                    rows.Add(row);
                }
            }
        }

        foreach (var prop in shaderInfo)
        {
            if (prop.Kind == PropertyKind.Vector)
                continue;

            if (prop.Name == "_CustomColors") continue;
            if (prop.Name == "_Color" && customColorsProp is not null) continue;

            bool isSplit = snapshot is not null && snapshot.IsPropertySplit(matName, prop.Name);

            if (prop.HasAttribute("MaterialToggle"))
            {
                if (!isSplit)
                    rows.Add(toggleBuilder.BuildSharedRow(material, matName, prop, snapshot!, parent));
                else
                    rows.AddRange(toggleBuilder.BuildSplitRows(material, matName, prop, snapshot!, parent));
            }
            else if (prop.Kind == PropertyKind.Range)
            {
                if (!isSplit)
                    rows.Add(floatBuilder.BuildSharedRangeRow(material, matName, prop, snapshot!, parent));
                else
                    rows.AddRange(floatBuilder.BuildSplitRangeRows(material, matName, prop, snapshot!, parent));
            }
            else if (prop.Kind == PropertyKind.Float)
            {
                if (!isSplit)
                    rows.Add(floatBuilder.BuildSharedFloatRow(material, matName, prop, snapshot!, parent));
                else
                    rows.AddRange(floatBuilder.BuildSplitFloatRows(material, matName, prop, snapshot!, parent));
            }
            else if (prop.Kind == PropertyKind.Color)
            {
                if (!isSplit)
                {
                    var row = colorBuilder.BuildSharedRow(material, matName, prop, snapshot!, parent, canvasRoot);
                    rows.Add(row);
                    if (prop.Name == "_Color")
                    {
                        colorRows.Add(row);
                        row.SetActive(overrideColorOn);
                    }
                }
                else
                {
                    var splitRows = colorBuilder.BuildSplitRows(material, matName, prop, snapshot!, parent, canvasRoot);
                    foreach (var row in splitRows)
                    {
                        rows.Add(row);
                        if (prop.Name == "_Color")
                        {
                            colorRows.Add(row);
                            row.SetActive(overrideColorOn);
                        }
                    }
                }
            }
            else if (prop.Kind == PropertyKind.Texture)
            {
                if (!isSplit)
                    rows.Add(textureBuilder.BuildSharedRow(material, matName, prop, snapshot!, parent, canvasRoot));
                else
                    rows.AddRange(textureBuilder.BuildSplitRows(material, matName, prop, snapshot!, parent, canvasRoot));
            }
        }

        return rows;
    }

    private GameObject BuildOverrideColorRow(Material material, string matName,
        ConfigSnapshot snapshot, ShaderProperty customColorsProp,
        bool overrideColorOn, List<GameObject> colorRows,
        RectTransform parent, RectTransform canvasRoot, SaberHand sourceHand)
    {
        var toggle = new UIToggle().SetValue(overrideColorOn);
        var mat = material;
        var pid = customColorsProp.Id;
        var capturedColorRows = colorRows;
        var originalColor = materialController.GetOriginalColor(matName, "_Color");

        UIColorPicker? inlinePickerRef = null;

        toggle.OnValueChanged(v =>
        {
            mat.SetFloat(pid, v ? 0 : 1);
            var leftMat = materialController.FindMaterialOnHand(matName, SaberHand.Left);
            var rightMat = materialController.FindMaterialOnHand(matName, SaberHand.Right);
            if (leftMat != null && leftMat != mat) leftMat.SetFloat(pid, v ? 0 : 1);
            if (rightMat != null && rightMat != mat) rightMat.SetFloat(pid, v ? 0 : 1);

            foreach (var row in capturedColorRows)
                row?.SetActive(v);
            if (v && inlinePickerRef is not null && mat.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId))
            {
                var overrideColor = inlinePickerRef.GetColor();
                mat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, overrideColor);
                if (leftMat != null && leftMat != mat) leftMat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, overrideColor);
                if (rightMat != null && rightMat != mat) rightMat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, overrideColor);
            }
            else if (!v && mat.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId))
            {
                mat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, originalColor);
                if (leftMat != null && leftMat != mat) leftMat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, originalColor);
                if (rightMat != null && rightMat != mat) rightMat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, originalColor);
            }
            materialController.Snapshot(matName, mat, sourceHand);
        });

        var cbRow = new HBox("  Override colorCR").SetParent(parent);
        cbRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        cbRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        cbRow.LayoutGroup.childForceExpandHeight = false;
        toggle.SetParent(cbRow.RectTransform);
        var overrideLbl = new UILabel("OverrideColorL", "  Override color")
            .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
            .SetAlignment(TMPro.TextAlignmentOptions.Left)
            .SetParent(cbRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        splitPopup.MakeLabelInteractive(overrideLbl, matName, "_Color", snapshot, toggle);
        UILayoutFactory.AddRowHitArea(cbRow.RectTransform, toggle);

        if (mat.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId))
        {
            bool colorIsSplit = snapshot is not null && snapshot.IsPropertySplit(matName, "_Color");
            if (!colorIsSplit)
            {
                var inlineColor = mat.GetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId);
                var capturedToggle = toggle;
                var inlinePicker = new UIColorPicker("InlineCP_" + matName, canvasRoot)
                    .SetColor(inlineColor)
                    .OnColorChanged(c =>
                    {
                        if (!capturedToggle.IsOn) return;

                        mat.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, c);
                        var leftClone = materialController.FindMaterialOnHand(matName, SaberHand.Left);
                        var rightClone = materialController.FindMaterialOnHand(matName, SaberHand.Right);
                        if (leftClone != null && leftClone != mat) leftClone.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, c);
                        if (rightClone != null && rightClone != mat) rightClone.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, c);
                        materialController.RefreshPropertyBlocks();
                    })
                    .OnCommit(c =>
                    {
                        materialController.Snapshot(matName, mat, sourceHand);
                    });
                inlinePicker.SetResetColor(materialController.GetOriginalColor(matName, "_Color"));
                inlinePicker.SetParent(cbRow.RectTransform).AddLayoutElement(preferredWidth: UITheme.SwatchWidth, preferredHeight: UITheme.SwatchHeight);
                inlinePickerRef = inlinePicker;
            }
        }

        return cbRow.GameObject;
    }

    private static ShaderProperty? FindProperty(IReadOnlyList<ShaderProperty> info, string name)
    {
        foreach (var p in info)
            if (p.Name == name) return p;
        return null;
    }
}