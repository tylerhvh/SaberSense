// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIMultiComboBox : UIElement
{
    private UIImage _buttonBorder;
    private UIImage _buttonFill;
    private UILabel _buttonLabel;

    private bool _isOpen;
    private List<string> _options = [];
    private HashSet<int> _selectedIndices = [];
    private bool _isUpdating;

    public IReadOnlyCollection<int> SelectedIndices => _selectedIndices;

    private Action<HashSet<int>>? _onSelectionChanged;

    private readonly Dictionary<int, List<GameObject>> _dependentElements = new();
    private readonly List<GameObject> _showWhenAny = [];

    private List<Image> _itemBgs = [];

    private GameObject _backdropGO = null!;
    private GameObject _popupGO = null!;
    private RectTransform _popupRect = null!;

    private readonly RectTransform? _canvasRoot;

    public UIMultiComboBox(string name = "MultiComboBox", RectTransform? canvasRoot = null) : base(name)
    {
        _canvasRoot = canvasRoot;

        var le = RectTransform.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 3.166f;
        le.preferredHeight = 3.166f;

        _buttonBorder = new UIImage("Border")
            .SetColor(new Color(0.04f, 0.04f, 0.04f, 1f))
            .SetParent(this, false);
        _buttonBorder.SetAnchors(Vector2.zero, Vector2.one);
        _buttonBorder.ImageComponent.raycastTarget = true;

        _buttonFill = new UIImage("Fill");
        _buttonFill.RectTransform.SetParent(_buttonBorder.RectTransform, false);
        _buttonFill.SetAnchors(Vector2.zero, Vector2.one);
        _buttonFill.RectTransform.offsetMin = new Vector2(0.2f, 0.2f);
        _buttonFill.RectTransform.offsetMax = new Vector2(-0.2f, -0.2f);
        _buttonFill.ImageComponent.raycastTarget = false;
        _buttonFill.SetSprite(UIGradient.CmbNormal);
        _buttonFill.ImageComponent.type = Image.Type.Simple;
        _buttonFill.ImageComponent.color = Color.white;

        _buttonLabel = new UILabel("Text", "-")
            .SetFontSize(UITheme.FontNormal)
            .SetColor(new Color(0.59f, 0.59f, 0.59f, 1f))
            .SetAlignment(TMPro.TextAlignmentOptions.Left);
        _buttonLabel.RectTransform.SetParent(_buttonBorder.RectTransform, false);
        _buttonLabel.SetAnchors(Vector2.zero, Vector2.one);
        _buttonLabel.RectTransform.offsetMin = new Vector2(1, 0);
        _buttonLabel.RectTransform.offsetMax = new Vector2(-3, 0);
        _buttonLabel.TextComponent.raycastTarget = false;

        var arrow = new UILabel("Arrow", "▼")
            .SetFontSize(UITheme.FontSmall)
            .SetColor(new Color(0.59f, 0.59f, 0.59f, 1f));
        arrow.RectTransform.SetParent(_buttonBorder.RectTransform, false);
        arrow.SetAnchors(new Vector2(0.85f, 0), Vector2.one);
        arrow.TextComponent.raycastTarget = false;

        var handler = _buttonBorder.AddComponent<PointerEventHandler>();
        handler.OnClick = ToggleDropdown;
        handler.OnEnter = () => _buttonFill.SetSprite(UIGradient.CmbHover);
        handler.OnExit = () => { if (!_isOpen) _buttonFill.SetSprite(UIGradient.CmbNormal); };

        _backdropGO = CreateBackdrop();
        _popupGO = CreatePopup();
    }

    public UIMultiComboBox SetOptions(List<string> options)
    {
        _options = options;
        RebuildItems();
        UpdateButtonLabel();
        return this;
    }

    public UIMultiComboBox SetSelected(IEnumerable<int> indices)
    {
        if (_isUpdating) return this;
        _isUpdating = true;
        try
        {
            _selectedIndices = [.. indices];
            UpdateVisuals();
            UpdateButtonLabel();
            SyncDependentVisibility();
            UICallbackGuard.Invoke(_onSelectionChanged!, _selectedIndices);
        }
        finally { _isUpdating = false; }
        return this;
    }

    public UIMultiComboBox OnSelectionChanged(Action<HashSet<int>> callback)
    {
        _onSelectionChanged += callback;
        return this;
    }

    public UIMultiComboBox ControlsVisibility(int index, GameObject target)
    {
        if (!_dependentElements.ContainsKey(index))
            _dependentElements[index] = [];
        _dependentElements[index].Add(target);
        target.SetActive(_selectedIndices.Contains(index));
        return this;
    }

    public UIMultiComboBox ShowWhenAnySelected(GameObject target)
    {
        _showWhenAny.Add(target);
        target.SetActive(_selectedIndices.Count is > 0);
        return this;
    }

    private void ToggleDropdown()
    {
        if (_isOpen) Close();
        else Open();
    }

    private void Open()
    {
        if (_canvasRoot == null) return;
        _isOpen = true;
        _buttonFill.SetSprite(UIGradient.CmbHover);

        _backdropGO.transform.SetParent(_canvasRoot, false);
        var bRect = _backdropGO.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;

        _popupGO.transform.SetParent(_canvasRoot, false);
        _popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        _popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        float dropH = (_options.Count * 3.333f) + 0.333f;

        _popupRect.sizeDelta = new Vector2(_popupRect.sizeDelta.x, dropH);

        RepositionPopup();

        _backdropGO.SetActive(true);
        _popupGO.SetActive(true);
        _backdropGO.transform.SetAsLastSibling();
        _popupGO.transform.SetAsLastSibling();

        for (int i = 0; i < _itemBgs.Count; i++)
            _itemBgs[i].color = UITheme.SurfaceLight;

        UpdateVisuals();

        var tracker = _popupGO.GetComponent<PopupTracker>();
        if (tracker == null)
        {
            tracker = _popupGO.AddComponent<PopupTracker>();
            tracker.Owner = this;
        }
        tracker.enabled = true;
    }

    private void RepositionPopup()
    {
        if (_canvasRoot == null) return;

        var rootRect = _canvasRoot.rect;
        float pivotOffsetX = (0.5f - _canvasRoot.pivot.x) * rootRect.width;
        float pivotOffsetY = (0.5f - _canvasRoot.pivot.y) * rootRect.height;

        var corners = new Vector3[4];
        RectTransform.GetWorldCorners(corners);
        Vector3 rawBL = _canvasRoot.InverseTransformPoint(corners[0]);
        Vector3 rawBR = _canvasRoot.InverseTransformPoint(corners[3]);
        Vector2 localBL = new(rawBL.x - pivotOffsetX, rawBL.y - pivotOffsetY);
        float width = rawBR.x - rawBL.x;
        float dropH = _popupRect.sizeDelta.y;

        _popupRect.sizeDelta = new Vector2(width, dropH);

        float canvasBottom = -rootRect.height * _canvasRoot.pivot.y;
        bool openUp = (localBL.y - 0.5f - dropH) < canvasBottom;

        if (openUp)
        {
            Vector3 rawTL = _canvasRoot.InverseTransformPoint(corners[1]);
            Vector2 localTL = new(rawTL.x - pivotOffsetX, rawTL.y - pivotOffsetY);
            _popupRect.pivot = new Vector2(0f, 0f);
            _popupRect.anchoredPosition = new Vector2(localBL.x, localTL.y + 0.5f);
        }
        else
        {
            _popupRect.pivot = new Vector2(0f, 1f);
            _popupRect.anchoredPosition = new Vector2(localBL.x, localBL.y - 0.5f);
        }
    }

    private void Close()
    {
        _isOpen = false;
        _buttonFill.SetSprite(UIGradient.CmbNormal);
        _backdropGO.SetActive(false);
        _popupGO.SetActive(false);
        _backdropGO.transform.SetParent(RectTransform, false);
        _popupGO.transform.SetParent(RectTransform, false);

        var tracker = _popupGO.GetComponent<PopupTracker>();
        if (tracker != null) tracker.enabled = false;
    }

    private sealed class PopupTracker : MonoBehaviour
    {
        internal UIMultiComboBox? Owner;
        private void LateUpdate() => Owner?.RepositionPopup();
    }

    private GameObject CreateBackdrop()
        => UIPopupHelper.CreateBackdrop("MBackdrop", _canvasRoot!, RectTransform, Close);

    private GameObject CreatePopup()
    {
        var go = UIPopupHelper.CreatePopupContainer("MPopup", _canvasRoot!, RectTransform, out _popupRect, out _);
        return go;
    }

    private void RebuildItems()
    {
        var container = _popupRect.childCount > 0 ? _popupRect.GetChild(0) : null;
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
        _itemBgs.Clear();

        for (int i = 0; i < _options.Count; i++)
        {
            int idx = i;
            string opt = _options[i];

            var itemGO = new GameObject("Item_" + i);
            itemGO.transform.SetParent(container, false);
            itemGO.AddComponent<RectTransform>();
            itemGO.AddComponent<LayoutElement>().preferredHeight = 3.333f;

            var itemBg = itemGO.AddComponent<Image>();
            itemBg.material = UIMaterials.NoBloomMaterial;
            itemBg.color = UITheme.SurfaceLight;
            itemBg.raycastTarget = true;
            _itemBgs.Add(itemBg);

            var itemLabel = new UILabel("Lbl", opt)
                .SetFontSize(UITheme.FontSmall)
                .SetColor(UITheme.TextSecondary)
                .SetAlignment(TMPro.TextAlignmentOptions.Left);
            itemLabel.RectTransform.SetParent(itemGO.transform, false);
            itemLabel.SetAnchors(Vector2.zero, Vector2.one);
            itemLabel.RectTransform.offsetMin = new Vector2(1, 0);
            itemLabel.RectTransform.offsetMax = new Vector2(-1, 0);
            itemLabel.TextComponent.raycastTarget = false;

            var handler = itemGO.AddComponent<PointerEventHandler>();
            handler.OnEnter = () =>
            {
                itemBg.color = UITheme.SurfacePressed;
            };
            handler.OnExit = () =>
            {
                itemBg.color = UITheme.SurfaceLight;
            };
            handler.OnClick = () => ToggleItem(idx);
        }
    }

    private void ToggleItem(int idx)
    {
        if (_selectedIndices.Contains(idx))
            _selectedIndices.Remove(idx);
        else
            _selectedIndices.Add(idx);

        UpdateVisuals();
        UpdateButtonLabel();
        SyncDependentVisibility();
        UICallbackGuard.Invoke(_onSelectionChanged!, _selectedIndices);
    }

    private void SyncDependentVisibility()
    {
        foreach (var kvp in _dependentElements)
        {
            bool active = _selectedIndices.Contains(kvp.Key);
            foreach (var go in kvp.Value)
                if (go != null) go.SetActive(active);
        }
        bool anyActive = _selectedIndices.Count is > 0;
        foreach (var go in _showWhenAny)
            if (go != null) go.SetActive(anyActive);
    }

    private void UpdateButtonLabel()
    {
        if (_selectedIndices.Count is 0)
        {
            _buttonLabel.SetText("-");
            return;
        }

        var names = _selectedIndices
            .Where(i => i >= 0 && i < _options.Count)
            .Select(i => _options[i]);
        string text = string.Join(", ", names);

        if (text.Length > 25)
            text = text[..22] + "...";

        _buttonLabel.SetText(text);
    }

    private void UpdateVisuals()
    {
        var container = _popupRect.childCount > 0 ? _popupRect.GetChild(0) : null;
        if (container == null) return;

        for (int i = 0; i < _options.Count; i++)
        {
            if (i >= container.childCount) break;

            var child = container.GetChild(i);
            var txt = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null)
            {
                bool sel = _selectedIndices.Contains(i);
                txt.color = sel ? UITheme.Accent : UITheme.TextSecondary;
                txt.fontStyle = sel ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            }
        }
    }
}