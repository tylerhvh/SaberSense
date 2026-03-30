// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System;
using UnityEngine;

namespace SaberSense.GUI.Framework.Menu;

internal sealed class SplitPopupManager
{
    private readonly MaterialEditingController _materialController;
    private readonly SaberSelectionController _selectionController;
    private readonly Newtonsoft.Json.JsonSerializer _json;

    private GameObject? _splitPopupBackdrop;
    private GameObject? _splitPopupPanel;

    public Action? OnPropertyChanged;

    public RectTransform? CanvasRoot { get; set; }

    public SplitPopupManager(
        MaterialEditingController materialController,
        SaberSelectionController selectionController,
        Catalog.Data.IJsonProvider jsonProvider)
    {
        _materialController = materialController;
        _selectionController = selectionController;
        _json = jsonProvider.Json;
    }

    public void MakeLabelInteractive(UILabel label, string matName, string propName,
        ConfigSnapshot snapshot, UIToggle? toggle = null)
    {
        if (snapshot is null) return;
        label.TextComponent.raycastTarget = true;
        var handler = label.GameObject.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = e =>
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                Show(label.RectTransform, matName, propName, snapshot);
            else if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
                toggle?.InvokeToggle();
        };
        var hoverDetector = label.GameObject.AddComponent<SplitKeyHoverDetector>();
        hoverDetector.MatName = matName;
        hoverDetector.PropName = propName;
        hoverDetector.Snapshot = snapshot;
        hoverDetector.PopupManager = this;
        hoverDetector.LabelRT = label.RectTransform;
    }

    public void MakeLabelInteractiveInRow(GameObject rowGO, string matName, string propName,
        ConfigSnapshot snapshot)
    {
        if (snapshot is null) return;
        var lbl = rowGO.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (lbl == null) return;
        var rt = lbl.GetComponent<RectTransform>();
        lbl.raycastTarget = true;
        var handler = lbl.gameObject.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = e =>
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                Show(rt, matName, propName, snapshot);
        };
        var hoverDetector = lbl.gameObject.AddComponent<SplitKeyHoverDetector>();
        hoverDetector.MatName = matName;
        hoverDetector.PropName = propName;
        hoverDetector.Snapshot = snapshot;
        hoverDetector.PopupManager = this;
        hoverDetector.LabelRT = rt;
    }

    public void MakeLabelInteractiveInPropRow(UIPropRow row, string matName, string propName,
        ConfigSnapshot snapshot)
    {
        if (snapshot is null || row?.Label is null) return;
        var lbl = row.Label;
        lbl.TextComponent.raycastTarget = true;
        var handler = lbl.GameObject.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = e =>
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                Show(lbl.RectTransform, matName, propName, snapshot);
        };
        var hoverDetector = lbl.GameObject.AddComponent<SplitKeyHoverDetector>();
        hoverDetector.MatName = matName;
        hoverDetector.PropName = propName;
        hoverDetector.Snapshot = snapshot;
        hoverDetector.PopupManager = this;
        hoverDetector.LabelRT = lbl.RectTransform;
    }

    public void Show(RectTransform anchorRT, string matName, string propName,
        ConfigSnapshot snapshot)
    {
        if (CanvasRoot == null || snapshot is null) return;
        Hide();

        bool isSplit = snapshot.IsPropertySplit(matName, propName);

        _splitPopupBackdrop = new GameObject("SplitBackdrop");
        _splitPopupBackdrop.AddComponent<RectTransform>();
        var bImg = _splitPopupBackdrop.AddComponent<UnityEngine.UI.Image>();
        bImg.material = UIMaterials.NoBloomMaterial;
        bImg.color = new Color(0, 0, 0, 0.01f);
        bImg.raycastTarget = true;
        UIPopupHelper.SetupPopupCanvas(_splitPopupBackdrop, CanvasRoot, 102);
        _splitPopupBackdrop.AddComponent<PointerEventHandler>().OnClick = Hide;
        _splitPopupBackdrop.transform.SetParent(CanvasRoot, false);
        var bRect = _splitPopupBackdrop.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        bRect.anchoredPosition = Vector2.zero;
        _splitPopupBackdrop.transform.SetAsLastSibling();

        _splitPopupPanel = new GameObject("SplitPanel");
        var panelRect = _splitPopupPanel.AddComponent<RectTransform>();
        UIPopupHelper.SetupPopupCanvas(_splitPopupPanel, CanvasRoot, 103);
        _splitPopupPanel.transform.SetParent(CanvasRoot, false);

        var corners = new Vector3[4];
        anchorRT.GetWorldCorners(corners);
        Vector3 localBL = CanvasRoot.InverseTransformPoint(corners[0]);

        float btnW = 14f;
        float btnH = 3.3f;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(btnW, btnH);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(localBL.x, localBL.y);

        var borderImg = _splitPopupPanel.AddComponent<UnityEngine.UI.Image>();
        borderImg.material = UIMaterials.NoBloomMaterial;
        borderImg.color = new Color32(10, 10, 10, 255);
        borderImg.raycastTarget = true;

        var vlg = _splitPopupPanel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.spacing = 0.15f;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        if (!isSplit)
        {
            CreateContextButton("Split", _splitPopupPanel.transform, () =>
            {
                if (!snapshot.MaterialOverrides.ContainsKey(matName))
                {
                    var activeMat = _materialController.FindLiveMaterial(matName);
                    if (activeMat != null) _materialController.Snapshot(matName, activeMat);
                }

                if (snapshot.MaterialOverrides.ContainsKey(matName) && snapshot.MaterialOverrides[matName][propName] is null)
                {
                    var liveMat = _materialController.FindLiveMaterial(matName);
                    if (liveMat != null)
                    {
                        var shaderInfo = _materialController.GetShaderInfo(liveMat.shader);
                        if (shaderInfo is not null)
                        {
                            foreach (var p in shaderInfo)
                            {
                                if (p.Name == propName)
                                {
                                    var json = MaterialPropertyCodec.Encode(p, liveMat, _json);
                                    if (json is not null) snapshot.MaterialOverrides[matName][propName] = json;
                                    break;
                                }
                            }
                        }
                    }
                }
                _materialController.SplitProperty(_selectionController.SelectedEntry!, matName, propName);
                Hide();
                OnPropertyChanged?.Invoke();
            });
        }
        else
        {
            CreateContextButton("Unsplit", _splitPopupPanel.transform, () =>
            {
                _materialController.UnsplitProperty(_selectionController.SelectedEntry!, matName, propName);
                Hide();
                OnPropertyChanged?.Invoke();
            });
        }

        _splitPopupPanel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_splitPopupBackdrop != null) { UnityEngine.Object.Destroy(_splitPopupBackdrop); _splitPopupBackdrop = null; }
        if (_splitPopupPanel != null) { UnityEngine.Object.Destroy(_splitPopupPanel); _splitPopupPanel = null; }
    }

    private static void CreateContextButton(string text, Transform parent, Action onClick)
    {
        var go = new GameObject(text + "Btn");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.material = UIMaterials.NoBloomMaterial;
        bg.color = new Color32(35, 35, 35, 255);
        bg.raycastTarget = true;
        var label = new UILabel(text + "L", text).SetFontSize(UITheme.FontSmall)
            .SetColor(new Color32(200, 200, 200, 255)).SetAlignment(TMPro.TextAlignmentOptions.Left);
        label.RectTransform.SetParent(go.transform, false);
        label.SetAnchors(Vector2.zero, Vector2.one);
        label.RectTransform.offsetMin = new Vector2(1.5f, 0);
        label.RectTransform.offsetMax = Vector2.zero;
        var handler = go.AddComponent<PointerEventHandler>();
        handler.OnEnter = () => bg.color = new Color32(25, 25, 25, 255);
        handler.OnExit = () => bg.color = new Color32(35, 35, 35, 255);
        handler.OnClick = () => onClick?.Invoke();
    }
}