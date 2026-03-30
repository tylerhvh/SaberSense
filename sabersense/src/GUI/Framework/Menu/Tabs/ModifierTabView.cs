// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Data;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Builders;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Profiles.SaberAsset;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Menu.Tabs;

internal sealed class ModifierTabView : IDisposable
{
    private readonly SaberSelectionController _selectionController;
    private readonly MaterialEditingController _materialController;
    private readonly MaterialPropertyRowBuilder _rowBuilder;
    private readonly PreviewSession _previewSession;
    private readonly SaberLoadout _saberSet;
    private readonly IMessageBroker _broker;
    private readonly IJsonProvider _jsonProvider;

    private RectTransform _modifierListRoot = null!;
    private readonly List<GameObject> _modifierRows = [];
    private GameObject _rightColGO = null!;

    private RectTransform _saberMaterialListRoot = null!;
    private readonly List<GameObject> _materialRows = [];
    private UILabel _noMaterialsLabel = null!;
    private BaseButton _revertButton = null!;

    private RectTransform _canvasRoot = null!;

    public ModifierTabView(
        SaberSelectionController selectionController,
        MaterialEditingController materialController,
        MaterialPropertyRowBuilder rowBuilder,
        PreviewSession previewSession,
        SaberLoadout saberSet,
        IMessageBroker broker,
        IJsonProvider jsonProvider)
    {
        _selectionController = selectionController;
        _materialController = materialController;
        _rowBuilder = rowBuilder;
        _previewSession = previewSession;
        _saberSet = saberSet;
        _broker = broker;
        _jsonProvider = jsonProvider;
    }

    private void NotifyModified()
    {
        var entry = _selectionController.SelectedEntry;
        if (entry is not null)
        {
            var focused = entry[_previewSession.FocusedHand];
            var other = entry[_previewSession.FocusedHand.Other()];
            if (focused?.ComponentModifiers is not null && other?.ComponentModifiers is not null
                && other != focused)
            {
                other.ComponentModifiers.SyncFrom(focused.ComponentModifiers);
            }

            CaptureModifiersToSnapshot(_saberSet.Left, entry[SaberHand.Left]!);
            CaptureModifiersToSnapshot(_saberSet.Right, entry[SaberHand.Right]!);
        }
    }

    private void CaptureModifiersToSnapshot(SaberProfile profile, PieceDefinition piece)
    {
        if (profile?.Snapshot is null || piece?.ComponentModifiers is null) return;
        _ = profile.Snapshot.CaptureModifierState(piece.ComponentModifiers, _jsonProvider);
    }

    public GameObject Build(RectTransform parent, RectTransform canvasRoot)
    {
        _canvasRoot = canvasRoot;

        var root = UILayoutFactory.TabRoot("ModifierTab", parent);

        var columns = new HBox("ModTabCols").SetParent(root);
        UnityEngine.Object.Destroy(columns.GameObject.GetComponent<ContentSizeFitter>());
        columns.SetSpacing(UITheme.ColumnGap).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var leftCol = new VBox("LeftCol").SetParent(columns.RectTransform);
        UnityEngine.Object.Destroy(leftCol.GameObject.GetComponent<ContentSizeFitter>());
        leftCol.SetSpacing(UITheme.GroupGap).SetPadding(0, 0, 0, 0).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var matsGroup = new UIGroupBox("Saber materials");
        matsGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _saberMaterialListRoot = new VBox("MaterialListRoot").SetParent(matsGroup.Content).RectTransform;
        var matLayout = _saberMaterialListRoot.GetComponent<VerticalLayoutGroup>();
        matLayout.childForceExpandHeight = false;
        matLayout.childControlHeight = true;
        _saberMaterialListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _noMaterialsLabel = new UILabel("NoMats", "No editable materials.")
            .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextMuted)
            .SetAlignment(TMPro.TextAlignmentOptions.Center)
            .SetParent(_saberMaterialListRoot)
            .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _revertButton = new BaseButton("Revert").SetParent(matsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        _revertButton.GameObject.SetActive(false);
        _revertButton.OnClick = () =>
        {
            var entry = _selectionController.SelectedEntry;
            if (entry is null) return;

            _saberSet.Left.Snapshot?.MaterialOverrides.Clear();
            _saberSet.Right.Snapshot?.MaterialOverrides.Clear();

            _broker?.Publish(new PreviewSaberChangedMsg(entry));
        };

        var rightCol = new VBox("RightCol").SetParent(columns.RectTransform);
        UnityEngine.Object.Destroy(rightCol.GameObject.GetComponent<ContentSizeFitter>());
        rightCol.SetSpacing(UITheme.GroupGap).SetPadding(0, 0, 0, 0).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _rightColGO = rightCol.GameObject;
        _rightColGO.SetActive(false);

        var modsGroup = new UIGroupBox("Custom modifiers");
        modsGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _modifierListRoot = new VBox("ModifierListRoot").SetParent(modsGroup.Content).RectTransform;
        var layout = _modifierListRoot.GetComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        _modifierListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var modRevertBtn = new BaseButton("Revert").SetParent(modsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        modRevertBtn.OnClick = () =>
        {
            var mcm = _previewSession?.ActiveRenderer?.Definition?.ComponentModifiers;
            if (mcm is null) return;
            foreach (var mod in mcm.AllBindings()) mod.Reset();
            NotifyModified();
            RefreshModifiers();
        };

        BindEvents();
        return root.gameObject;
    }

    private IDisposable? _selectionChangedSub;

    private void BindEvents()
    {
        _selectionChangedSub = _broker?.Subscribe<SaberEquippedMsg>(msg => OnSelectionChanged(msg.Entry))!;
    }

    private void OnSelectionChanged(SaberAssetEntry entry)
    {
    }

    public void RefreshModifiers()
    {
        foreach (var row in _modifierRows) UnityEngine.Object.Destroy(row);
        _modifierRows.Clear();
        List<UISlider> sliders = [];

        var mcm = _previewSession?.ActiveRenderer?.Definition?.ComponentModifiers;
        bool hasModifiers = mcm is not null && mcm.HasModifiers && mcm.AllBindings().Count is > 0;
        if (_rightColGO != null) _rightColGO.SetActive(hasModifiers);
        if (!hasModifiers) return;

        var focusedHand = _previewSession?.FocusedHand ?? SaberHand.Left;
        var snapshot = _saberSet[focusedHand].Snapshot;
        var modsJson = snapshot?.ModifierState?["bindings"] as JObject;

        foreach (var mod in mcm!.AllBindings())
        {
            var modJson = modsJson?[mod.Id.ToString()] as JObject;

            if (mod is SaberSense.Behaviors.VisibilityBinding visMod)
            {
                bool visible = modJson?["Visible"]?.ToObject<bool>() ?? visMod.Visible;

                var toggle = new UIToggle().SetValue(visible);
                toggle.OnValueChanged(val =>
                {
                    visMod.Visible = val;
                    NotifyModified();
                });

                var row = new HBox(mod.Name + "CR").SetParent(_modifierListRoot);
                row.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
                row.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
                row.LayoutGroup.childForceExpandHeight = false;
                toggle.SetParent(row.RectTransform);
                new UILabel(mod.Name + "L", mod.Name).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
                    .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
                UILayoutFactory.AddRowHitArea(row.RectTransform, toggle);

                _modifierRows.Add(row.GameObject);
            }
            else if (mod is SaberSense.Behaviors.SpatialBinding transMod)
            {
                var pos = modJson?["PositionOffset"]?.ToObject<Vector3>() ?? transMod.PositionOffset;
                var scale = modJson?["ScaleOffset"]?.ToObject<Vector3>() ?? transMod.ScaleOffset;
                var rotation = modJson?["RotationOffset"]?.ToObject<float>() ?? transMod.RotationOffset;

                var labelRow = new HBox(mod.Name + "Label").SetParent(_modifierListRoot);
                labelRow.SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.SectionLabelHeight, flexibleWidth: 1);
                labelRow.LayoutGroup.childAlignment = TextAnchor.LowerLeft;
                new UILabel(mod.Name + "L", mod.Name).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextHeader)
                    .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(labelRow.RectTransform);
                _modifierRows.Add(labelRow.GameObject);

                var posX = new UISlider().SetRange(-0.5f, 0.5f).SetValue(pos.x);
                posX.OnValueChanged(val => { var p = transMod.PositionOffset; p.x = val; transMod.PositionOffset = p; });
                posX.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Pos X", posX, _modifierListRoot));
                sliders.Add(posX);

                var posY = new UISlider().SetRange(-0.5f, 0.5f).SetValue(pos.y);
                posY.OnValueChanged(val => { var p = transMod.PositionOffset; p.y = val; transMod.PositionOffset = p; });
                posY.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Pos Y", posY, _modifierListRoot));
                sliders.Add(posY);

                var posZ = new UISlider().SetRange(-0.5f, 0.5f).SetValue(pos.z);
                posZ.OnValueChanged(val => { var p = transMod.PositionOffset; p.z = val; transMod.PositionOffset = p; });
                posZ.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Pos Z", posZ, _modifierListRoot));
                sliders.Add(posZ);

                var rot = new UISlider().SetRange(-180f, 180f).SetValue(rotation);
                rot.OnValueChanged(val => { transMod.RotationOffset = val; });
                rot.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Rotation", rot, _modifierListRoot));
                sliders.Add(rot);

                var scaleX = new UISlider().SetRange(-1f, 1f).SetValue(scale.x);
                scaleX.OnValueChanged(val => { var s = transMod.ScaleOffset; s.x = val; transMod.ScaleOffset = s; });
                scaleX.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Scale X", scaleX, _modifierListRoot));
                sliders.Add(scaleX);

                var scaleY = new UISlider().SetRange(-1f, 1f).SetValue(scale.y);
                scaleY.OnValueChanged(val => { var s = transMod.ScaleOffset; s.y = val; transMod.ScaleOffset = s; });
                scaleY.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Scale Y", scaleY, _modifierListRoot));
                sliders.Add(scaleY);

                var scaleZ = new UISlider().SetRange(-1f, 1f).SetValue(scale.z);
                scaleZ.OnValueChanged(val => { var s = transMod.ScaleOffset; s.z = val; transMod.ScaleOffset = s; });
                scaleZ.OnCommit(val => NotifyModified());
                _modifierRows.Add(UILayoutFactory.SliderRow("  Scale Z", scaleZ, _modifierListRoot));
                sliders.Add(scaleZ);
            }
        }
        Canvas.ForceUpdateCanvases();

        foreach (var s in sliders) s.ForceClipEvaluation();
    }

    public void RefreshMaterials()
    {
        if (_saberMaterialListRoot == null) return;

        foreach (var row in _materialRows) UnityEngine.Object.Destroy(row);
        _materialRows.Clear();

        var entry = _selectionController.SelectedEntry;
        if (entry?.LeftPiece is SaberAssetDefinition def)
            _materialController.SnapshotOriginals(def, entry.LeftPiece.Asset?.RelativePath!);

        var leftSaber = _previewSession?.Sabers?.Left;
        PieceRenderer? masterRenderer = null;
        if (leftSaber is not null && leftSaber.Pieces.TryGet(AssetTypeTag.SaberAsset, out var piece))
            masterRenderer = piece;
        if (masterRenderer?.GameObject == null)
        {
            if (_noMaterialsLabel is not null) _noMaterialsLabel.GameObject.SetActive(true);
            if (_revertButton is not null) _revertButton.GameObject.SetActive(false);
            return;
        }

        var renderers = new List<Renderer>();
        masterRenderer.GameObject.GetComponentsInChildren(true, renderers);

        const SaberHand editHand = SaberHand.Left;
        List<(string name, Material mat, Renderer rend, int slot, SaberHand hand)> editableMaterials = [];
        var nameResolver = new SaberSense.Core.Utilities.MaterialNameResolver();
        HashSet<string> leftRawNames = [];

        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].shader == null) continue;
                var shaderInfo = _materialController.GetShaderInfo(mats[i].shader);
                if (shaderInfo is null || shaderInfo.Count is 0) continue;

                string baseName = nameResolver.Resolve(mats[i]);

                leftRawNames.Add(SaberSense.Core.Utilities.MaterialNameResolver.StripUnityNameSuffixes(mats[i].name));

                var handMat = _materialController.FindMaterialOnHand(baseName, editHand) ?? mats[i];
                editableMaterials.Add((baseName, handMat, rend, i, editHand));
            }
        }

        var rightSaber = _previewSession?.Sabers?.Right;
        PieceRenderer? rightRenderer = null;
        if (rightSaber is not null && rightSaber.Pieces.TryGet(AssetTypeTag.SaberAsset, out var rightPiece))
            rightRenderer = rightPiece;
        if (rightRenderer?.GameObject != null)
        {
            var rightRenderers = new List<Renderer>();
            rightRenderer.GameObject.GetComponentsInChildren(true, rightRenderers);
            var rightNameResolver = new SaberSense.Core.Utilities.MaterialNameResolver();

            foreach (var rend in rightRenderers)
            {
                if (rend == null) continue;
                var mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == null) continue;
                    var shaderInfo = _materialController.GetShaderInfo(mats[i].shader);
                    if (shaderInfo is null || shaderInfo.Count is 0) continue;

                    string rawName = SaberSense.Core.Utilities.MaterialNameResolver.StripUnityNameSuffixes(mats[i].name);

                    if (leftRawNames.Contains(rawName)) continue;

                    string baseName = rightNameResolver.Resolve(mats[i]);
                    editableMaterials.Add((baseName, mats[i], rend, i, SaberHand.Right));
                }
            }
        }

        if (editableMaterials.Count is 0)
        {
            if (_noMaterialsLabel is not null) _noMaterialsLabel.GameObject.SetActive(true);
            if (_revertButton is not null) _revertButton.GameObject.SetActive(false);
            return;
        }

        if (_noMaterialsLabel is not null) _noMaterialsLabel.GameObject.SetActive(false);
        if (_revertButton is not null) _revertButton.GameObject.SetActive(true);

        foreach (var (name, material, rend, slot, hand) in editableMaterials)
        {
            var displayName = hand == SaberHand.Right ? $"{name} [R]" : name;
            var section = new UICollapsibleSection(displayName);
            section.SetParent(_saberMaterialListRoot);
            _materialRows.Add(section.GameObject);

            var shaderInfo = _materialController.GetShaderInfo(material.shader);
            var snapshot = _saberSet[hand].Snapshot;
            var propertyRows = _rowBuilder.BuildPropertyRows(material, name, snapshot!, shaderInfo!,
                section.Content, _canvasRoot);
            _materialRows.AddRange(propertyRows);
        }
        Canvas.ForceUpdateCanvases();
    }

    public void Dispose()
    {
        _selectionChangedSub?.Dispose();
    }
}