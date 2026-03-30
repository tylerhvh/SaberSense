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

internal sealed class ColorPropertyBuilder : MaterialPropertyBuilderBase
{
    public ColorPropertyBuilder(
        MaterialEditingController materialController,
        SplitPopupManager splitPopup,
        SaberSelectionController selectionController,
        Catalog.Data.IJsonProvider jsonProvider,
        PreviewSession previewSession)
        : base(materialController, splitPopup, selectionController, jsonProvider, previewSession) { }

    public GameObject BuildSharedRow(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot,
        RectTransform parent, RectTransform canvasRoot)
    {
        var color = ResolveColor(snapshot, matName, prop.Name);
        var mat = material;
        var pid = prop.Id;
        var capturedHand = SourceHand;
        var colorPicker = new UIColorPicker("CP_" + matName + prop.Name, canvasRoot)
            .SetColor(color)
            .OnColorChanged(c =>
            {
                mat.SetColor(pid, c);
                var leftMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Left);
                var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right);
                if (leftMat != null && leftMat != mat) leftMat.SetColor(pid, c);
                if (rightMat != null && rightMat != mat) rightMat.SetColor(pid, c);
            })
            .OnCommit(c =>
            {
                MaterialController.Snapshot(matName, mat, capturedHand);
            });
        colorPicker.SetResetColor(MaterialController.GetOriginalColor(matName, prop.Name));

        var row = new UIPropRow(prop.Description, colorPicker, 10);
        row.RectTransform.SetParent(parent, false);
        row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);
        SplitPopup.MakeLabelInteractiveInPropRow(row, matName, prop.Name, snapshot);
        return row.GameObject;
    }

    public List<GameObject> BuildSplitRows(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot,
        RectTransform parent, RectTransform canvasRoot)
    {
        var pid = prop.Id;

        var leftMat = material;
        var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right) ?? material;

        return BuildSplitRows(material, matName, prop, snapshot, parent,
            (handVal, hand, tag) =>
            {
                var targetMat = hand == SaberHand.Left ? leftMat : rightMat;
                Color color;
                if (handVal is not null)
                    color = handVal.ToObject<Color>(Json);
                else
                    color = targetMat.GetColor(pid);
                var capturedHand = hand;
                var colorPicker = new UIColorPicker("CP_" + matName + prop.Name + tag, canvasRoot)
                    .SetColor(color)
                    .OnColorChanged(c =>
                    {
                        targetMat.SetColor(pid, c);
                        MaterialController.SnapshotSplit(matName, prop.Name,
                            Newtonsoft.Json.Linq.JToken.FromObject(c, Json),
                            capturedHand);
                    });
                colorPicker.SetResetColor(MaterialController.GetOriginalColor(matName, prop.Name));

                var row = new UIPropRow(prop.Description + " " + tag, colorPicker, 10);
                row.RectTransform.SetParent(parent, false);
                row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);
                SplitPopup.MakeLabelInteractiveInPropRow(row, matName, prop.Name, snapshot);
                return row.GameObject;
            });
    }
}