// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace SaberSense.GUI.Framework.Core;

public static class UIPopupHelper
{
    public static GameObject CreateBackdrop(string name, RectTransform canvasRoot, RectTransform parent, System.Action onClose, float alpha = 0f)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.material = UIMaterials.NoBloomMaterial;
        img.color = new Color(0, 0, 0, alpha);
        img.raycastTarget = true;
        SetupPopupCanvas(go, canvasRoot, 100);
        go.AddComponent<PointerEventHandler>().OnClick = onClose;
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        return go;
    }

    public static GameObject CreatePopupContainer(
        string name, RectTransform canvasRoot, RectTransform parent,
        out RectTransform popupRect, out VerticalLayoutGroup layout)
    {
        var go = new GameObject(name);
        popupRect = go.AddComponent<RectTransform>();

        SetupPopupCanvas(go, canvasRoot, 101);

        var bgI = go.AddComponent<Image>();
        bgI.material = UIMaterials.NoBloomMaterial;
        bgI.color = UITheme.Border;
        bgI.raycastTarget = true;

        var containerGO = new GameObject("ItemContainer");
        containerGO.transform.SetParent(popupRect, false);
        var ctrRect = containerGO.AddComponent<RectTransform>();
        ctrRect.anchorMin = Vector2.zero;
        ctrRect.anchorMax = Vector2.one;
        ctrRect.offsetMin = new Vector2(0.166f, 0.166f);
        ctrRect.offsetMax = new Vector2(-0.166f, -0.166f);

        layout = containerGO.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0;
        layout.padding = new RectOffset(0, 0, 0, 0);

        go.SetActive(false);
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void SetupPopupCanvas(GameObject go, RectTransform? canvasRoot, int sortingOrder)
    {
        var subCanvas = go.AddComponent<Canvas>();
        subCanvas.overrideSorting = true;
        subCanvas.sortingOrder = sortingOrder;

        var parentVrgr = canvasRoot != null ? canvasRoot.GetComponent<VRGraphicRaycaster>() : null;
        if (parentVrgr != null)
        {
            var vrgr = go.AddComponent<VRGraphicRaycaster>();
            VRRaycasterHelper.CopyPhysicsRaycaster(parentVrgr, vrgr);
        }
        else
        {
            go.AddComponent<GraphicRaycaster>();
        }
    }
}