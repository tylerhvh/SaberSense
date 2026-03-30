// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIColorPicker : UIElement
{
    private const float PanelSize = 36f;
    private const float ContextMenuButtonWidth = 14f;
    private const float ContextMenuButtonHeight = 3.3f;
    private const float ContextMenuSpacing = 0.15f;
    private const float ContextMenuLabelInset = 1.5f;

    public UIImage SwatchBorder { get; private set; }
    public UIImage SwatchFill { get; private set; }

    internal float Hue;
    internal float Sat = 1f;
    internal float Val = 1f;
    internal float Alpha = 1f;
    internal bool IsExpanded;

    private Action<Color>? _onColorChanged;
    private Action<Color>? _onCommit;
    private readonly RectTransform? _canvasRoot;

    private GameObject _backdropGO = null!;
    private GameObject _expandGO = null!;
    private RectTransform _expandRect = null!;
    private ColorPickerPanel _panel = null!;

    private static Color? _clipboard;

    private Color _initialColor = Color.white;
    private bool _hasResetColor;

    private GameObject? _ctxBackdropGO;
    private GameObject? _ctxPanelGO;

    public UIColorPicker(string name = "ColorPicker", RectTransform? canvasRoot = null) : base(name)
    {
        _canvasRoot = canvasRoot;

        SwatchBorder = new UIImage("Border")
            .SetColor(new Color32(30, 30, 30, 255))
            .SetParent(this, false);
        SwatchBorder.ImageComponent.type = Image.Type.Simple;

        SwatchBorder.RectTransform.anchorMin = new Vector2(1f, 0.15f);
        SwatchBorder.RectTransform.anchorMax = new Vector2(1f, 0.85f);
        SwatchBorder.RectTransform.pivot = new Vector2(1f, 0.5f);
        SwatchBorder.RectTransform.sizeDelta = new Vector2(4f, 0f);
        SwatchBorder.ImageComponent.raycastTarget = true;

        SwatchFill = new UIImage("Fill")
            .SetColor(Color.HSVToRGB(0, 1, 1))
            .SetParent(SwatchBorder, false);
        SwatchFill.ImageComponent.type = Image.Type.Simple;
        SwatchFill.SetAnchors(new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        SwatchFill.ImageComponent.raycastTarget = false;

        var handler = SwatchBorder.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = (eventData) =>
        {
            if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                ShowContextMenu();
            else
                ToggleExpand();
        };

        var hoverDetector = SwatchBorder.AddComponent<ActionKeyHoverDetector>();
        hoverDetector.Picker = this;

        _backdropGO = UIPopupHelper.CreateBackdrop("ColorBackdrop", _canvasRoot!, RectTransform, Collapse, alpha: 0.01f);

        _expandGO = new GameObject("ColorPanel");
        _expandRect = _expandGO.AddComponent<RectTransform>();
        UIPopupHelper.SetupPopupCanvas(_expandGO, _canvasRoot, 101);
        _expandGO.SetActive(false);
        _expandGO.transform.SetParent(RectTransform, false);

        var panelGO = new GameObject("PanelRenderer");
        panelGO.transform.SetParent(_expandRect, false);
        var prt = panelGO.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.sizeDelta = Vector2.zero;

        _panel = panelGO.AddComponent<ColorPickerPanel>();
        _panel.Picker = this;
        _panel.material = UIMaterials.NoBloomMaterial;
        _panel.raycastTarget = true;

        var input = panelGO.AddComponent<ColorPickerInput>();
        input.Picker = this;
        input.Panel = _panel;
    }

    public UIColorPicker OnColorChanged(Action<Color> callback)
    {
        _onColorChanged = callback;
        return this;
    }

    public UIColorPicker OnCommit(Action<Color> callback)
    {
        _onCommit = callback;
        return this;
    }

    internal void NotifyCommit()
    {
        if (_onCommit is null) return;
        Color c = Color.HSVToRGB(Hue, Sat, Val);
        c.a = Alpha;
        UICallbackGuard.Invoke(_onCommit, c);
    }

    public UIColorPicker SetColor(Color color)
    {
        if (!_hasResetColor) { _initialColor = color; _hasResetColor = true; }
        Alpha = color.a;
        Color.RGBToHSV(new Color(color.r, color.g, color.b, 1f), out Hue, out Sat, out Val);
        SwatchFill.SetColor(color);
        if (_panel != null) _panel.SetVerticesDirty();
        return this;
    }

    public UIColorPicker SetResetColor(Color color)
    {
        _initialColor = color;
        _hasResetColor = true;
        return this;
    }

    internal void NotifyColorChanged()
    {
        Color c = Color.HSVToRGB(Hue, Sat, Val);
        c.a = Alpha;
        SwatchFill.SetColor(c);
        UICallbackGuard.Invoke(_onColorChanged!, c);
        if (_panel != null) _panel.SetVerticesDirty();
    }

    public Color GetColor()
    {
        Color c = Color.HSVToRGB(Hue, Sat, Val);
        c.a = Alpha;
        return c;
    }

    private void ToggleExpand()
    {
        if (IsExpanded) Collapse();
        else Expand();
    }

    private void Expand()
    {
        if (_canvasRoot == null) return;
        IsExpanded = true;

        var corners = new Vector3[4];
        RectTransform.GetWorldCorners(corners);
        Vector3 localBL = _canvasRoot.InverseTransformPoint(corners[0]);
        Vector3 localBR = _canvasRoot.InverseTransformPoint(corners[3]);

        _backdropGO.transform.SetParent(_canvasRoot, false);
        var bRect = _backdropGO.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        bRect.anchoredPosition = Vector2.zero;
        _backdropGO.SetActive(true);
        _backdropGO.transform.SetAsLastSibling();

        _expandGO.transform.SetParent(_canvasRoot, false);
        _expandRect.anchorMin = new Vector2(0.5f, 0.5f);
        _expandRect.anchorMax = new Vector2(0.5f, 0.5f);
        _expandRect.sizeDelta = new Vector2(PanelSize, PanelSize);

        Rect canvasRect = _canvasRoot.rect;
        float panelH = PanelSize;
        float bottomEdge = localBL.y - panelH;

        if (bottomEdge >= canvasRect.yMin)
        {
            _expandRect.pivot = new Vector2(1f, 1f);
            _expandRect.anchoredPosition = new Vector2(localBR.x, localBL.y);
        }
        else
        {
            Vector3 localTR = _canvasRoot.InverseTransformPoint(corners[2]);
            _expandRect.pivot = new Vector2(1f, 0f);
            _expandRect.anchoredPosition = new Vector2(localBR.x, localTR.y);
        }

        _expandGO.SetActive(true);
        _expandGO.transform.SetAsLastSibling();

        if (_panel != null) _panel.SetVerticesDirty();
    }

    private void Collapse()
    {
        IsExpanded = false;
        _backdropGO.SetActive(false);
        _expandGO.SetActive(false);
        _backdropGO.transform.SetParent(RectTransform, false);
        _expandGO.transform.SetParent(RectTransform, false);
    }

    internal void ShowContextMenu()
    {
        if (_canvasRoot == null) return;
        HideContextMenu();

        _ctxBackdropGO = new GameObject("CtxBackdrop");
        _ctxBackdropGO.AddComponent<RectTransform>();
        var bImg = _ctxBackdropGO.AddComponent<Image>();
        bImg.material = UIMaterials.NoBloomMaterial;
        bImg.color = new Color(0, 0, 0, 0.01f);
        bImg.raycastTarget = true;
        UIPopupHelper.SetupPopupCanvas(_ctxBackdropGO, _canvasRoot, 102);
        _ctxBackdropGO.AddComponent<PointerEventHandler>().OnClick = HideContextMenu;
        _ctxBackdropGO.transform.SetParent(_canvasRoot, false);
        var ctxBRect = _ctxBackdropGO.GetComponent<RectTransform>();
        ctxBRect.anchorMin = Vector2.zero;
        ctxBRect.anchorMax = Vector2.one;
        ctxBRect.sizeDelta = Vector2.zero;
        ctxBRect.anchoredPosition = Vector2.zero;
        _ctxBackdropGO.transform.SetAsLastSibling();

        _ctxPanelGO = new GameObject("CtxPanel");
        var ctxRect = _ctxPanelGO.AddComponent<RectTransform>();
        UIPopupHelper.SetupPopupCanvas(_ctxPanelGO, _canvasRoot, 103);
        _ctxPanelGO.transform.SetParent(_canvasRoot, false);

        var corners = new Vector3[4];
        SwatchBorder.RectTransform.GetWorldCorners(corners);
        Vector3 localBL = _canvasRoot.InverseTransformPoint(corners[0]);
        Vector3 localBR = _canvasRoot.InverseTransformPoint(corners[3]);

        float btnW = ContextMenuButtonWidth;
        float btnH = ContextMenuButtonHeight;
        float totalH = btnH * 3;

        ctxRect.anchorMin = new Vector2(0.5f, 0.5f);
        ctxRect.anchorMax = new Vector2(0.5f, 0.5f);
        ctxRect.sizeDelta = new Vector2(btnW, totalH);
        ctxRect.pivot = new Vector2(1f, 1f);
        ctxRect.anchoredPosition = new Vector2(localBR.x, localBL.y);

        var borderImg = _ctxPanelGO.AddComponent<Image>();
        borderImg.material = UIMaterials.NoBloomMaterial;
        borderImg.color = new Color32(10, 10, 10, 255);
        borderImg.raycastTarget = true;

        var vlg = _ctxPanelGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.spacing = ContextMenuSpacing;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        CreateContextButton("Copy", _ctxPanelGO.transform, () =>
        {
            _clipboard = GetColor();
            HideContextMenu();
        });

        CreateContextButton("Paste", _ctxPanelGO.transform, () =>
        {
            if (_clipboard.HasValue)
            {
                SetColor(_clipboard.Value);
                NotifyColorChanged();
                NotifyCommit();
            }
            HideContextMenu();
        });

        CreateContextButton("Reset", _ctxPanelGO.transform, () =>
        {
            SetColor(_initialColor);
            NotifyColorChanged();
            NotifyCommit();
            HideContextMenu();
        });

        _ctxPanelGO.transform.SetAsLastSibling();
    }

    private void HideContextMenu()
    {
        if (_ctxBackdropGO != null) { UnityEngine.Object.Destroy(_ctxBackdropGO); _ctxBackdropGO = null; }
        if (_ctxPanelGO != null) { UnityEngine.Object.Destroy(_ctxPanelGO); _ctxPanelGO = null; }
    }

    private static void CreateContextButton(string text, Transform parent, Action onClick)
    {
        var go = new GameObject(text + "Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();

        var bg = go.AddComponent<Image>();
        bg.material = UIMaterials.NoBloomMaterial;
        bg.color = new Color32(35, 35, 35, 255);
        bg.raycastTarget = true;

        var label = new UILabel(text + "L", text)
            .SetFontSize(UITheme.FontSmall)
            .SetColor(new Color32(200, 200, 200, 255))
            .SetAlignment(TMPro.TextAlignmentOptions.Left);
        label.RectTransform.SetParent(rt, false);
        label.SetAnchors(Vector2.zero, Vector2.one);
        label.RectTransform.offsetMin = new Vector2(ContextMenuLabelInset, 0);
        label.RectTransform.offsetMax = Vector2.zero;

        var handler = go.AddComponent<PointerEventHandler>();
        handler.OnEnter = () => bg.color = new Color32(25, 25, 25, 255);
        handler.OnExit = () => bg.color = new Color32(35, 35, 35, 255);
        handler.OnClick = () => onClick?.Invoke();
    }

    internal sealed class ActionKeyHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public UIColorPicker? Picker;
        private bool _hovered;

        public void OnPointerEnter(PointerEventData e) => _hovered = true;
        public void OnPointerExit(PointerEventData e) => _hovered = false;

        private void Update()
        {
            if (_hovered && Picker is not null && SaberSense.Core.ActionKeyInputBehavior.IsPressedDown)
                Picker.ShowContextMenu();
        }
    }
}

internal sealed class ColorPickerPanel : Graphic
{
    public UIColorPicker? Picker;

    internal const float SvL = 0.028f, SvR = 0.861f, SvB = 0.111f, SvT = 0.972f;
    internal const float HuL = 0.883f, HuR = 0.972f, HuB = SvB, HuT = SvT;
    internal const float AlL = 0.028f, AlR = 0.861f, AlB = 0.028f, AlT = 0.089f;

    private const int SvGrid = 16;
    private const int HueSegs = 32;

    private const float OuterBorderFrac = 0.006f;
    private const float InnerBorderFrac = 0.011f;

    private const float SvCursorFrac = 0.018f;
    private const float SvCursorInnerScale = 0.6f;
    private const float HueCursorFrac = 0.015f;
    private const float HueCursorInsetFrac = 0.06f;
    private const float HueCursorInnerScale = 0.55f;
    private const float HueCursorInnerInsetScale = 1.5f;
    private const float AlphaCursorFrac = 0.015f;
    private const float AlphaCursorInsetFrac = 0.08f;
    private const float AlphaCursorInnerScale = 0.55f;
    private const float AlphaCursorInnerInsetScale = 1.5f;

    private static readonly Color32 CursorFillColor = new(255, 255, 255, 180);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Picker is null) return;

        Rect r = rectTransform.rect;
        float W = r.width, H = r.height;
        float X0 = r.xMin, Y0 = r.yMin;

        AddQuad(vh, X0, Y0, X0 + W, Y0 + H, C(10, 10, 10));

        float b1 = W * OuterBorderFrac;
        AddQuad(vh, X0 + b1, Y0 + b1, X0 + W - b1, Y0 + H - b1, C(60, 60, 60));

        float b2 = W * InnerBorderFrac;
        AddQuad(vh, X0 + b2, Y0 + b2, X0 + W - b2, Y0 + H - b2, C(40, 40, 40));

        float svBL = X0 + W * (SvL - OuterBorderFrac), svBR = X0 + W * (SvR + OuterBorderFrac);
        float svBB = Y0 + H * (SvB - OuterBorderFrac), svBT = Y0 + H * (SvT + OuterBorderFrac);
        AddQuad(vh, svBL, svBB, svBR, svBT, C(10, 10, 10));

        float svL = X0 + W * SvL, svR2 = X0 + W * SvR;
        float svB2 = Y0 + H * SvB, svT2 = Y0 + H * SvT;
        Color hueCol = Color.HSVToRGB(Picker.Hue, 1f, 1f);

        int svStart = vh.currentVertCount;
        int cols = SvGrid + 1;
        for (int iy = 0; iy <= SvGrid; iy++)
        {
            float ty = (float)iy / SvGrid;
            float y = Mathf.Lerp(svB2, svT2, ty);
            for (int ix = 0; ix <= SvGrid; ix++)
            {
                float tx = (float)ix / SvGrid;
                float x = Mathf.Lerp(svL, svR2, tx);
                Color baseC = Color.Lerp(Color.white, hueCol, tx);
                Color finalC = Color.Lerp(Color.black, baseC, ty);
                AddVert(vh, x, y, (Color32)finalC);
            }
        }
        for (int iy = 0; iy < SvGrid; iy++)
        {
            for (int ix = 0; ix < SvGrid; ix++)
            {
                int bl = svStart + iy * cols + ix;
                int br = bl + 1;
                int tl = bl + cols;
                int tr = tl + 1;
                vh.AddTriangle(bl, tl, tr);
                vh.AddTriangle(bl, tr, br);
            }
        }

        float huBL = X0 + W * (HuL - OuterBorderFrac), huBR = X0 + W * (HuR + OuterBorderFrac);
        float huBB = Y0 + H * (HuB - OuterBorderFrac), huBT = Y0 + H * (HuT + OuterBorderFrac);
        AddQuad(vh, huBL, huBB, huBR, huBT, C(10, 10, 10));

        float huL2 = X0 + W * HuL, huR2 = X0 + W * HuR;
        float huB2 = Y0 + H * HuB, huT2 = Y0 + H * HuT;

        int hueStart = vh.currentVertCount;
        for (int i = 0; i <= HueSegs; i++)
        {
            float t = (float)i / HueSegs;
            float y = Mathf.Lerp(huB2, huT2, t);

            Color hc = Color.HSVToRGB(1f - t, 1f, 1f);
            Color32 c32 = hc;
            AddVert(vh, huL2, y, c32);
            AddVert(vh, huR2, y, c32);
        }
        for (int i = 0; i < HueSegs; i++)
        {
            int bL = hueStart + i * 2;
            int bR = bL + 1;
            int tL = bL + 2;
            int tR = bL + 3;
            vh.AddTriangle(bL, tL, tR);
            vh.AddTriangle(bL, tR, bR);
        }

        float alBL = X0 + W * (AlL - OuterBorderFrac), alBR = X0 + W * (AlR + OuterBorderFrac);
        float alBB = Y0 + H * (AlB - OuterBorderFrac), alBT = Y0 + H * (AlT + OuterBorderFrac);
        AddQuad(vh, alBL, alBB, alBR, alBT, C(10, 10, 10));

        float alL2 = X0 + W * AlL, alR2 = X0 + W * AlR;
        float alB2 = Y0 + H * AlB, alT2 = Y0 + H * AlT;
        Color alphaC = Color.HSVToRGB(Picker.Hue, Picker.Sat, Picker.Val);
        Color32 alDark = C(0, 0, 0);
        Color32 alFull = (Color32)alphaC;
        int alStart = vh.currentVertCount;
        AddVert(vh, alL2, alB2, alDark);
        AddVert(vh, alL2, alT2, alDark);
        AddVert(vh, alR2, alT2, alFull);
        AddVert(vh, alR2, alB2, alFull);
        vh.AddTriangle(alStart, alStart + 1, alStart + 2);
        vh.AddTriangle(alStart, alStart + 2, alStart + 3);

        {
            float cx = Mathf.Lerp(svL, svR2, Picker.Sat);
            float cy = Mathf.Lerp(svB2, svT2, Picker.Val);
            float cs = W * SvCursorFrac;
            AddQuad(vh, cx - cs, cy - cs, cx + cs, cy + cs, C(10, 10, 10));
            float ci = cs * SvCursorInnerScale;
            AddQuad(vh, cx - ci, cy - ci, cx + ci, cy + ci, CursorFillColor);
        }

        {
            float hy = Mathf.Lerp(huB2, huT2, 1f - Picker.Hue);
            float cs = H * HueCursorFrac;
            float inset = (huR2 - huL2) * HueCursorInsetFrac;
            AddQuad(vh, huL2 + inset, hy - cs, huR2 - inset, hy + cs, C(10, 10, 10));
            float ci = cs * HueCursorInnerScale;
            float ini = inset * HueCursorInnerInsetScale;
            AddQuad(vh, huL2 + ini, hy - ci, huR2 - ini, hy + ci, CursorFillColor);
        }

        {
            float ax = Mathf.Lerp(alL2, alR2, Picker.Alpha);
            float cs = W * AlphaCursorFrac;
            float inset = (alT2 - alB2) * AlphaCursorInsetFrac;
            AddQuad(vh, ax - cs, alB2 + inset, ax + cs, alT2 - inset, C(10, 10, 10));
            float ci = cs * AlphaCursorInnerScale;
            float ini = inset * AlphaCursorInnerInsetScale;
            AddQuad(vh, ax - ci, alB2 + ini, ax + ci, alT2 - ini, CursorFillColor);
        }
    }

    private static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);

    private static void AddVert(VertexHelper vh, float x, float y, Color32 col)
    {
        vh.AddVert(new Vector3(x, y, 0f), col, Vector4.zero);
    }

    private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 col)
    {
        int i = vh.currentVertCount;
        AddVert(vh, x0, y0, col);
        AddVert(vh, x0, y1, col);
        AddVert(vh, x1, y1, col);
        AddVert(vh, x1, y0, col);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    internal bool ScreenToFrac(PointerEventData eventData, out float fx, out float fy)
    {
        fx = fy = 0;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 lp))
            return false;

        Rect r = rectTransform.rect;
        fx = (lp.x - r.xMin) / r.width;
        fy = (lp.y - r.yMin) / r.height;
        return true;
    }
}

internal sealed class ColorPickerInput : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public UIColorPicker? Picker;
    public ColorPickerPanel? Panel;

    private enum Area { None, SV, Hue, Alpha }
    private Area _active;
    private bool _didDrag;

    public void OnInitializePotentialDrag(PointerEventData e)
    {
        if (e is not null) e.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData e) { _didDrag = false; HandleInput(e, true); }
    public void OnBeginDrag(PointerEventData e) { _didDrag = true; HandleInput(e, false); }
    public void OnDrag(PointerEventData e) => HandleInput(e, false);
    public void OnEndDrag(PointerEventData e) { _active = Area.None; Picker?.NotifyCommit(); }
    public void OnPointerUp(PointerEventData e) { if (!_didDrag) Picker?.NotifyCommit(); }

    private void HandleInput(PointerEventData e, bool isDown)
    {
        if (Picker is null || Panel is null) return;
        if (!Panel.ScreenToFrac(e, out float fx, out float fy)) return;

        if (isDown)
        {
            if (InRect(fx, fy, ColorPickerPanel.SvL, ColorPickerPanel.SvR,
                                ColorPickerPanel.SvB, ColorPickerPanel.SvT))
                _active = Area.SV;
            else if (InRect(fx, fy, ColorPickerPanel.HuL, ColorPickerPanel.HuR,
                                    ColorPickerPanel.HuB, ColorPickerPanel.HuT))
                _active = Area.Hue;
            else if (InRect(fx, fy, ColorPickerPanel.AlL, ColorPickerPanel.AlR,
                                    ColorPickerPanel.AlB, ColorPickerPanel.AlT))
                _active = Area.Alpha;
            else
                _active = Area.None;
        }

        switch (_active)
        {
            case Area.SV:
                Picker.Sat = Mathf.Clamp01(Remap(fx, ColorPickerPanel.SvL, ColorPickerPanel.SvR));
                Picker.Val = Mathf.Clamp01(Remap(fy, ColorPickerPanel.SvB, ColorPickerPanel.SvT));
                Picker.NotifyColorChanged();
                break;

            case Area.Hue:
                float ht = Remap(fy, ColorPickerPanel.HuB, ColorPickerPanel.HuT);
                Picker.Hue = Mathf.Clamp01(1f - ht);
                Picker.NotifyColorChanged();
                break;

            case Area.Alpha:
                Picker.Alpha = Mathf.Clamp01(Remap(fx, ColorPickerPanel.AlL, ColorPickerPanel.AlR));
                Picker.NotifyColorChanged();
                break;
        }
    }

    private static bool InRect(float fx, float fy, float l, float r, float b, float t)
        => fx >= l && fx <= r && fy >= b && fy <= t;

    private static float Remap(float v, float min, float max)
        => (v - min) / (max - min);
}