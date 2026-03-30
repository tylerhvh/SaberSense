// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UITabBar : UIElement
{
    private const float TabDividerWidth = 0.1f;
    private const float IndicatorHeight = 0.5f;
    private const float BottomLineHeight = 0.1f;

    private readonly List<TabCell> _tabs = [];
    private int _selectedIndex = -1;
    private Action<int, string>? _onTabChanged;

    public UITabBar(string name = "TabBar") : base(name)
    {
        var layout = GameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.spacing = TabDividerWidth;
        layout.padding = new RectOffset(0, 0, 0, 0);

        var bottomLine = new UIImage("BottomLine")
            .SetColor(UITheme.Divider);
        bottomLine.RectTransform.SetParent(RectTransform, false);
        bottomLine.RectTransform.anchorMin = new Vector2(0, 0);
        bottomLine.RectTransform.anchorMax = new Vector2(1, 0);
        bottomLine.RectTransform.pivot = new Vector2(0.5f, 0);
        bottomLine.RectTransform.sizeDelta = new Vector2(0, BottomLineHeight);
        bottomLine.ImageComponent.raycastTarget = false;
    }

    public UITabBar SetTabs(List<string> tabNames)
    {
        if (tabNames is null) return this;

        foreach (var tab in _tabs)
            UnityEngine.Object.Destroy(tab.Root);
        _tabs.Clear();

        for (int i = 0; i < tabNames.Count; i++)
        {
            int index = i;
            string tabName = tabNames[i];
            var cell = new TabCell(tabName, () => SelectTab(index));
            cell.Root.transform.SetParent(RectTransform, false);
            _tabs.Add(cell);
        }

        if (tabNames.Count is > 0) SelectTab(0);
        return this;
    }

    public UITabBar OnTabChanged(Action<int, string> callback)
    {
        _onTabChanged = callback;
        return this;
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].SetSelected(i == index);

        _selectedIndex = index;
        UICallbackGuard.Invoke(_onTabChanged!, index, _tabs[index].Name);
    }

    private sealed class TabCell
    {
        private static readonly Color32 BgSelected = UITheme.Surface;
        private static readonly Color32 BgUnselected = UITheme.SurfaceDark;
        private static readonly Color32 TextActive = UITheme.NavIconActive;
        private static readonly Color32 TextInactive = UITheme.NavIcon;
        private static readonly Color32 TextHover = UITheme.NavIconHover;

        public GameObject Root { get; }

        public string Name { get; }

        private readonly Image _bg;
        private readonly TMPro.TextMeshProUGUI _text;
        private readonly UIImage _indicator;
        private bool _selected;

        public TabCell(string name, Action onClick)
        {
            Name = name;

            Root = new GameObject("Tab_" + name);
            Root.AddComponent<RectTransform>();
            var containerLayout = Root.AddComponent<VerticalLayoutGroup>();
            containerLayout.childControlWidth = true;
            containerLayout.childControlHeight = true;
            containerLayout.childForceExpandWidth = true;
            containerLayout.spacing = 0;
            Root.AddComponent<LayoutElement>().flexibleWidth = 1;

            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(Root.transform, false);
            bodyGO.AddComponent<RectTransform>();
            bodyGO.AddComponent<LayoutElement>().flexibleHeight = 1;

            _bg = bodyGO.AddComponent<Image>();
            _bg.material = UIMaterials.NoBloomMaterial;
            _bg.color = BgUnselected;
            _bg.raycastTarget = true;

            var label = new UILabel("Lbl", name)
                .SetFontSize(UITheme.FontSmall)
                .SetColor(TextInactive);
            label.TextComponent.fontStyle = TMPro.FontStyles.Bold;
            label.TextComponent.alignment = TMPro.TextAlignmentOptions.Center;
            label.RectTransform.SetParent(bodyGO.transform, false);
            label.SetAnchors(Vector2.zero, Vector2.one);
            label.TextComponent.raycastTarget = false;
            _text = label.TextComponent;

            _indicator = new UIImage("Indicator")
                .SetColor(Color.clear);
            _indicator.RectTransform.SetParent(Root.transform, false);
            _indicator.AddLayoutElement(preferredHeight: IndicatorHeight);
            _indicator.ImageComponent.raycastTarget = false;

            var handler = bodyGO.AddComponent<PointerEventHandler>();
            handler.OnClick = onClick;
            handler.OnEnter = () =>
            {
                if (_selected) return;
                _text.color = TextHover;
                _bg.color = UITheme.SurfaceSubtle;
            };
            handler.OnExit = () =>
            {
                if (_selected) return;
                _text.color = TextInactive;
                _bg.color = BgUnselected;
            };
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            _bg.color = selected ? BgSelected : BgUnselected;
            _text.color = selected ? TextActive : TextInactive;
            _indicator.SetColor(selected ? UITheme.Accent : Color.clear);
        }
    }
}