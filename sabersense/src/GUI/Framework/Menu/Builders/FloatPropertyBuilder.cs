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

internal sealed class FloatPropertyBuilder : MaterialPropertyBuilderBase
{
    public FloatPropertyBuilder(
        MaterialEditingController materialController,
        SplitPopupManager splitPopup,
        SaberSelectionController selectionController,
        Catalog.Data.IJsonProvider jsonProvider,
        PreviewSession previewSession)
        : base(materialController, splitPopup, selectionController, jsonProvider, previewSession) { }

    public GameObject BuildSharedRangeRow(Material material, string matName,
        ShaderProperty rangeProp, ConfigSnapshot snapshot, RectTransform parent)
    {
        return BuildSharedSliderRow(material, matName, rangeProp, snapshot, parent,
            rangeProp.RangeMin ?? 0, rangeProp.RangeMax ?? 1);
    }

    public List<GameObject> BuildSplitRangeRows(Material material, string matName,
        ShaderProperty rangeProp, ConfigSnapshot snapshot, RectTransform parent)
    {
        return BuildSplitSliderRows(material, matName, rangeProp, snapshot, parent,
            rangeProp.RangeMin ?? 0, rangeProp.RangeMax ?? 1);
    }

    public GameObject BuildSharedFloatRow(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot, RectTransform parent)
    {
        return BuildSharedSliderRow(material, matName, prop, snapshot, parent, 0, 10);
    }

    public List<GameObject> BuildSplitFloatRows(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot, RectTransform parent)
    {
        return BuildSplitSliderRows(material, matName, prop, snapshot, parent, 0, 10);
    }

    private GameObject BuildSharedSliderRow(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot, RectTransform parent,
        float min, float max)
    {
        var val = ResolveFloat(snapshot, matName, prop.Name);
        var slider = new UISlider().SetRange(min, max).SetValue(val);
        var mat = material;
        var pid = prop.Id;
        var capturedHand = SourceHand;
        slider.OnValueChanged(v =>
        {
            mat.SetFloat(pid, v);
            var leftMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Left);
            var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right);
            if (leftMat != null && leftMat != mat) leftMat.SetFloat(pid, v);
            if (rightMat != null && rightMat != mat) rightMat.SetFloat(pid, v);
        });
        slider.OnCommit(v =>
        {
            MaterialController.Snapshot(matName, mat, capturedHand);
        });
        var rowGO = UILayoutFactory.SliderRow("  " + prop.Description, slider, parent);
        SplitPopup.MakeLabelInteractiveInRow(rowGO, matName, prop.Name, snapshot);
        return rowGO;
    }

    private List<GameObject> BuildSplitSliderRows(Material material, string matName,
        ShaderProperty prop, ConfigSnapshot snapshot, RectTransform parent,
        float min, float max)
    {
        var pid = prop.Id;

        var leftMat = material;
        var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right) ?? material;

        return BuildSplitRows(material, matName, prop, snapshot, parent,
            (handVal, hand, tag) =>
            {
                var targetMat = hand == SaberHand.Left ? leftMat : rightMat;
                float val = handVal is not null
                    ? handVal.ToObject<float>(Json)
                    : targetMat.GetFloat(pid);
                var slider = new UISlider().SetRange(min, max).SetValue(val);
                var capturedHand = hand;
                slider.OnValueChanged(v =>
                {
                    targetMat.SetFloat(pid, v);
                    MaterialController.SnapshotSplit(matName, prop.Name,
                        Newtonsoft.Json.Linq.JToken.FromObject(v), capturedHand);
                });
                var rowGO = UILayoutFactory.SliderRow("  " + prop.Description + " " + tag, slider, parent);
                SplitPopup.MakeLabelInteractiveInRow(rowGO, matName, prop.Name, snapshot);
                return rowGO;
            });
    }
}