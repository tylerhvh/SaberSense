// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIGroupBox : UIElement
{
    public RectTransform Content { get; private set; }

    public UIGroupBox(string title, string name = "GroupBox") : base(name)
    {
        float textStartX = -1;
        float textWidth = -1;

        var titleLabel = new UILabel("GroupTitle", title)
            .SetFontSize(UITheme.FontSmall)
            .SetColor(UITheme.TextLabel);
        titleLabel.TextComponent.fontStyle = TMPro.FontStyles.Bold;
        titleLabel.TextComponent.alignment = TMPro.TextAlignmentOptions.Left;

        if (!string.IsNullOrEmpty(title))
        {
            titleLabel.RectTransform.SetParent(RectTransform, false);
            titleLabel.RectTransform.anchorMin = new Vector2(0, 1);
            titleLabel.RectTransform.anchorMax = new Vector2(0, 1);
            titleLabel.RectTransform.pivot = new Vector2(0, 0.5f);

            textStartX = 4f;
            textWidth = titleLabel.TextComponent.GetPreferredValues(title).x;

            titleLabel.RectTransform.anchoredPosition = new Vector2(textStartX, 0);
            titleLabel.RectTransform.sizeDelta = new Vector2(textWidth, 4);

            textStartX -= 0.8f;
            textWidth += 1.6f;
        }

        var bg = new UIImage("GroupBg")
            .SetColor(UITheme.SurfaceInner);
        bg.RectTransform.SetParent(RectTransform, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.RectTransform.offsetMin = new Vector2(0.4f, 0.4f);
        bg.RectTransform.offsetMax = new Vector2(-0.4f, -0.4f);

        UIBorderUtils.DrawBorderLines("Outer", RectTransform, UITheme.Border, 0f, 0.2f, textStartX, textWidth);
        UIBorderUtils.DrawBorderLines("Inner", RectTransform, UITheme.Divider, 0.2f, 0.2f, textStartX, textWidth);

        if (!string.IsNullOrEmpty(title)) titleLabel.RectTransform.SetAsLastSibling();

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(bg.RectTransform, false);
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(4, 2);
        viewportRect.offsetMax = new Vector2(-5, -4);
        var maskImg = viewportGO.AddComponent<Image>();
        maskImg.color = Color.white;
        maskImg.material = UIMaterials.NoBloomMaterial;
        maskImg.raycastTarget = true;
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportRect, false);
        Content = contentGO.AddComponent<RectTransform>();
        Content.anchorMin = new Vector2(0, 1);
        Content.anchorMax = Vector2.one;
        Content.pivot = new Vector2(0.5f, 1f);
        Content.sizeDelta = Vector2.zero;

        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 2f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;

        var sizeFitter = contentGO.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scrollRect = bg.GameObject.AddComponent<VRSafeScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = Content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 5f;

        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(bg.RectTransform, false);
        var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.sizeDelta = new Vector2(1, 0);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.pivot = new Vector2(1, 0.5f);

        scrollbarRect.offsetMin = new Vector2(scrollbarRect.offsetMin.x, 0.15f);
        scrollbarRect.offsetMax = new Vector2(scrollbarRect.offsetMax.x, -0.3f);

        var scrollbarBg = scrollbarGO.AddComponent<Image>();
        scrollbarBg.color = UITheme.SurfaceHover;
        scrollbarBg.material = UIMaterials.NoBloomMaterial;
        scrollbarBg.raycastTarget = true;

        var hitPad = new GameObject("ScrollHitPad");
        hitPad.transform.SetParent(scrollbarGO.transform, false);
        var hitPadRect = hitPad.AddComponent<RectTransform>();
        hitPadRect.anchorMin = Vector2.zero;
        hitPadRect.anchorMax = Vector2.one;
        hitPadRect.offsetMin = new Vector2(-3f, 0);
        hitPadRect.offsetMax = Vector2.zero;
        var hitPadImg = hitPad.AddComponent<Image>();
        hitPadImg.color = new Color(0, 0, 0, 0);
        hitPadImg.material = UIMaterials.NoBloomMaterial;
        hitPadImg.raycastTarget = true;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.offsetMin = new Vector2(0.15f, 0);
        handleRect.offsetMax = new Vector2(-0.15f, 0);

        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = UITheme.ScrollHandle;
        handleImg.material = UIMaterials.NoBloomMaterial;

        var dummyHandleGO = new GameObject("DummyHandle");
        dummyHandleGO.transform.SetParent(scrollbarGO.transform, false);
        var dummyHandleRect = dummyHandleGO.AddComponent<RectTransform>();
        dummyHandleRect.anchorMin = Vector2.zero;
        dummyHandleRect.anchorMax = Vector2.one;
        dummyHandleRect.sizeDelta = Vector2.zero;

        var scrollbar = scrollbarGO.AddComponent<VRSafeScrollbar>();
        scrollbar.handleRect = dummyHandleRect;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;
        scrollbar.transition = Selectable.Transition.None;
        scrollbar.SetVisibleHandle(handleRect);

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = 0;

        var guard = bg.GameObject.AddComponent<ContentGuard>();
        guard.Init(scrollRect, viewportRect, Content);

        GameObject.AddComponent<CanvasAttachGuard>();
    }

    public UIGroupBox SizeToContent()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
        float contentHeight = LayoutUtility.GetPreferredHeight(Content);

        const float chromeHeight = 0.4f + 0.4f + 2f + 4f;
        float totalHeight = contentHeight + chromeHeight;

        var le = RectTransform.GetComponent<LayoutElement>() ?? RectTransform.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = totalHeight;
        return this;
    }

    internal sealed class VRSafeScrollRect : ScrollRect
    {
        public override void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject == null) return;
            if (content == null) { base.OnDrag(eventData); return; }

            Vector2 savedPos = content.anchoredPosition;
            base.OnDrag(eventData);

            var pos = content.anchoredPosition;
            bool corrupt = float.IsNaN(pos.x) || float.IsNaN(pos.y)
                || float.IsInfinity(pos.x) || float.IsInfinity(pos.y)
                || (pos - savedPos).sqrMagnitude > 10000f;

            if (corrupt)
                content.anchoredPosition = savedPos;
        }
    }

    internal sealed class VRSafeScrollbar : Scrollbar
    {
        private RectTransform? _visibleHandle;
        private Image? _visibleHandleImg;
        private float _lastValidValue;
        private HapticFeedbackManager? _hapticManager;
        private bool _hapticWasEnabled;

        private static readonly Color32 HandleNormal = UITheme.ScrollHandle;
        private static readonly Color32 HandleHovered = new(80, 80, 80, 255);

        public void SetVisibleHandle(RectTransform handle)
        {
            _visibleHandle = handle;
            _visibleHandleImg = handle.GetComponent<Image>();
            _hapticManager = FindObjectOfType<HapticFeedbackManager>();
            onValueChanged.AddListener(OnScrollbarValueChanged);
        }

        private void OnScrollbarValueChanged(float val)
        {
            if (float.IsNaN(val) || float.IsInfinity(val))
            {
                SetValueWithoutNotify(_lastValidValue);
                return;
            }
            _lastValidValue = val;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (_visibleHandleImg != null) _visibleHandleImg.color = HandleHovered;
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (_visibleHandleImg != null) _visibleHandleImg.color = HandleNormal;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            var trackRect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    trackRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                float trackHeight = trackRect.rect.height;
                if (trackHeight > 0)
                {
                    float normalized = Mathf.Clamp01((localPoint.y - trackRect.rect.yMin) / trackHeight);
                    value = normalized;
                }
            }

            if (_hapticManager != null)
            {
                _hapticWasEnabled = _hapticManager.hapticFeedbackEnabled;
                _hapticManager.hapticFeedbackEnabled = false;
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (_hapticManager != null)
                _hapticManager.hapticFeedbackEnabled = _hapticWasEnabled;
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject == null) return;
            base.OnDrag(eventData);
        }

        private void LateUpdate()
        {
            UpdateVisibleHandle();
        }

        private void UpdateVisibleHandle()
        {
            if (_visibleHandle == null) return;

            float v = _lastValidValue;
            float s = size;

            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            if (float.IsNaN(s) || float.IsInfinity(s)) s = 0.1f;

            v = Mathf.Clamp01(v);
            float handleSize = Mathf.Clamp(s, 0.05f, 1f);

            float handleBottom = v * (1f - handleSize);
            float handleTop = handleBottom + handleSize;
            _visibleHandle.anchorMin = new Vector2(_visibleHandle.anchorMin.x, handleBottom);
            _visibleHandle.anchorMax = new Vector2(_visibleHandle.anchorMax.x, handleTop);
        }
    }

    [DefaultExecutionOrder(1000)]
    internal sealed class ContentGuard : MonoBehaviour
    {
        private RectTransform? _content;

        public void Init(ScrollRect scrollRect, RectTransform viewport, RectTransform content)
        {
            _content = content;
        }

        private void LateUpdate()
        {
            if (_content == null) return;

            var pos = _content.anchoredPosition;
            bool corrupt = float.IsNaN(pos.x) || float.IsNaN(pos.y)
                || float.IsInfinity(pos.x) || float.IsInfinity(pos.y);

            if (corrupt)
                _content.anchoredPosition = new Vector2(
                    float.IsNaN(pos.x) || float.IsInfinity(pos.x) ? 0f : pos.x,
                    float.IsNaN(pos.y) || float.IsInfinity(pos.y) ? 0f : pos.y);
        }
    }

    [DefaultExecutionOrder(998)]
    internal sealed class CanvasAttachGuard : MonoBehaviour
    {
        private Graphic[] _graphics = null!;

        private void Awake()
        {
            _graphics = GetComponentsInChildren<Graphic>(true);
            foreach (var g in _graphics)
                g.enabled = false;
        }

        private void LateUpdate()
        {
            if (GetComponentInParent<Canvas>() != null)
            {
                foreach (var g in _graphics)
                    if (g != null) g.enabled = true;
                Destroy(this);
            }
        }
    }
}