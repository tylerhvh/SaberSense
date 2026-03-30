// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIComboBox : UIElement
{
    private UIImage _buttonBorder;
    private UIImage _buttonFill;
    private UILabel _buttonLabel;
    private PointerEventHandler _buttonHandler;

    private bool _isOpen;
    private List<string> _options = [];
    private int _selectedIndex = 0;
    private bool _openUpward;
    private Action<int, string>? _onSelect;

    private GameObject _backdropGO = null!;
    private GameObject _popupGO = null!;
    private RectTransform _popupRect = null!;
    private VerticalLayoutGroup _popupLayout = null!;

    private readonly RectTransform? _canvasRoot;

    public UIComboBox(string name = "ComboBox", RectTransform? canvasRoot = null) : base(name)
    {
        _canvasRoot = canvasRoot;

        var le = RectTransform.gameObject.AddComponent<LayoutElement>();
        le.minHeight = UISizes.ButtonHeight;
        le.preferredHeight = UISizes.ButtonHeight;

        _buttonBorder = new UIImage("Border")
            .SetColor(UITheme.Border)
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

        _buttonLabel = new UILabel("Text", "Select...")
            .SetFontSize(UITheme.FontSmall)
            .SetColor(UITheme.TextMuted)
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

        _buttonHandler = _buttonBorder.AddComponent<PointerEventHandler>();
        _buttonHandler.OnClick = ToggleDropdown;
        _buttonHandler.OnEnter = () => _buttonFill.SetSprite(UIGradient.CmbHover);
        _buttonHandler.OnExit = () => { if (!_isOpen) _buttonFill.SetSprite(UIGradient.CmbNormal); };

        _backdropGO = CreateBackdrop();
        _popupGO = CreatePopup();
    }

    public UIComboBox SetOptions(List<string> options)
    {
        _options = options;
        if (_options.Count is > 0 && _selectedIndex < _options.Count)
            _buttonLabel.SetText(_options[_selectedIndex]);
        RebuildItems();
        return this;
    }

    public UIComboBox SetSelected(int index)
    {
        if (_options.Count is 0) return this;
        _selectedIndex = index;
        _buttonLabel.SetText(_options[_selectedIndex]);
        return this;
    }

    public UIComboBox OnSelect(Action<int, string> callback)
    {
        _onSelect = callback;
        return this;
    }

    public UIComboBox SetOpenUpward(bool upward = true)
    {
        _openUpward = upward;
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

        var container = _popupRect.childCount > 0 ? _popupRect.GetChild(0) : null;
        if (container != null)
        {
            for (int j = 0; j < container.childCount; j++)
            {
                int itemIndex = j;
                bool isSelected = (itemIndex == _selectedIndex);
                var child = container.GetChild(j);

                var bgItem = child.GetComponent<Image>();
                if (bgItem != null) bgItem.color = UITheme.SurfaceLight;

                var txt = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.color = isSelected ? UITheme.Accent : UITheme.TextSecondary;
                    txt.fontStyle = isSelected ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
                }
            }
        }

        var rootRect = _canvasRoot.rect;
        float pivotOffsetX = (0.5f - _canvasRoot.pivot.x) * rootRect.width;
        float pivotOffsetY = (0.5f - _canvasRoot.pivot.y) * rootRect.height;

        var corners = new Vector3[4];
        RectTransform.GetWorldCorners(corners);
        Vector3 rawBL = _canvasRoot.InverseTransformPoint(corners[0]);
        Vector3 rawBR = _canvasRoot.InverseTransformPoint(corners[3]);
        Vector2 localBL = new(rawBL.x - pivotOffsetX, rawBL.y - pivotOffsetY);
        float width = rawBR.x - rawBL.x;

        float dropH = (_options.Count * 3.333f) + 0.333f;

        _backdropGO.transform.SetParent(_canvasRoot, false);
        var bRect = _backdropGO.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;

        _popupGO.transform.SetParent(_canvasRoot, false);
        _popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        _popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        _popupRect.sizeDelta = new Vector2(width, dropH);

        float canvasBottom = -_canvasRoot.rect.height * _canvasRoot.pivot.y;
        bool openUp = _openUpward || (localBL.y - 0.5f - dropH) < canvasBottom;

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

        _backdropGO.SetActive(true);
        _popupGO.SetActive(true);
        _backdropGO.transform.SetAsLastSibling();
        _popupGO.transform.SetAsLastSibling();
    }

    private void Close()
    {
        _isOpen = false;
        _buttonFill.SetSprite(UIGradient.CmbNormal);
        _backdropGO.SetActive(false);
        _popupGO.SetActive(false);
        _backdropGO.transform.SetParent(RectTransform, false);
        _popupGO.transform.SetParent(RectTransform, false);
    }

    private GameObject CreateBackdrop()
        => UIPopupHelper.CreateBackdrop("CBackdrop", _canvasRoot ?? RectTransform, RectTransform, Close);

    private GameObject CreatePopup()
    {
        var go = UIPopupHelper.CreatePopupContainer("CPopup", _canvasRoot ?? RectTransform, RectTransform, out _popupRect, out _popupLayout);
        return go;
    }

    private void RebuildItems()
    {
        var container = _popupRect.childCount > 0 ? _popupRect.GetChild(0) : null;
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(container.GetChild(i).gameObject);

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
            handler.OnClick = () =>
            {
                _selectedIndex = idx;
                _buttonLabel.SetText(opt);
                UICallbackGuard.Invoke(_onSelect!, idx, opt);
                Close();
            };
        }
    }
}