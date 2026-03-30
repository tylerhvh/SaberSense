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

internal sealed class TogglePropertyBuilder : MaterialPropertyBuilderBase
{
    public TogglePropertyBuilder(
        MaterialEditingController materialController,
        SplitPopupManager splitPopup,
        SaberSelectionController selectionController,
        Catalog.Data.IJsonProvider jsonProvider,
        PreviewSession previewSession)
        : base(materialController, splitPopup, selectionController, jsonProvider, previewSession) { }

    public GameObject BuildSharedRow(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot, RectTransform parent)
    {
        bool val = ResolveFloat(snapshot, matName, prop.Name) > 0;
        var toggle = new UIToggle().SetValue(val);
        var mat = material;
        var pid = prop.Id;
        var capturedHand = SourceHand;
        toggle.OnValueChanged(v =>
        {
            mat.SetFloat(pid, v ? 1 : 0);
            var leftMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Left);
            var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right);
            if (leftMat != null && leftMat != mat) leftMat.SetFloat(pid, v ? 1 : 0);
            if (rightMat != null && rightMat != mat) rightMat.SetFloat(pid, v ? 1 : 0);
            MaterialController.Snapshot(matName, mat, capturedHand);
        });

        var cbRow = new HBox("  " + prop.Description + "CR").SetParent(parent);
        cbRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        cbRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        cbRow.LayoutGroup.childForceExpandHeight = false;
        toggle.SetParent(cbRow.RectTransform);
        var lbl = new UILabel(prop.Description + "L", "  " + prop.Description)
            .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
            .SetAlignment(TMPro.TextAlignmentOptions.Left)
            .SetParent(cbRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        SplitPopup.MakeLabelInteractive(lbl, matName, prop.Name, snapshot, toggle);
        UILayoutFactory.AddRowHitArea(cbRow.RectTransform, toggle);
        return cbRow.GameObject;
    }

    public List<GameObject> BuildSplitRows(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot, RectTransform parent)
    {
        var pid = prop.Id;

        var leftMat = material;
        var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right) ?? material;

        return BuildSplitRows(material, matName, prop, snapshot, parent,
            (handVal, hand, tag) =>
            {
                var targetMat = hand == SaberHand.Left ? leftMat : rightMat;
                bool val = handVal is not null
                    ? handVal.ToObject<float>(Json) > 0
                    : targetMat.GetFloat(pid) > 0;
                var toggle = new UIToggle().SetValue(val);
                var capturedHand = hand;
                toggle.OnValueChanged(v =>
                {
                    targetMat.SetFloat(pid, v ? 1 : 0);
                    MaterialController.SnapshotSplit(matName, prop.Name,
                        Newtonsoft.Json.Linq.JToken.FromObject(v ? 1f : 0f), capturedHand);
                });

                var cbRow = new HBox("  " + prop.Description + tag + "CR").SetParent(parent);
                cbRow.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
                cbRow.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
                cbRow.LayoutGroup.childForceExpandHeight = false;
                toggle.SetParent(cbRow.RectTransform);
                var lbl = new UILabel(prop.Description + tag + "L", "  " + prop.Description + " " + tag)
                    .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
                    .SetAlignment(TMPro.TextAlignmentOptions.Left)
                    .SetParent(cbRow.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
                SplitPopup.MakeLabelInteractive(lbl, matName, prop.Name, snapshot, toggle);
                UILayoutFactory.AddRowHitArea(cbRow.RectTransform, toggle);
                return cbRow.GameObject;
            });
    }
}