// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HMUI;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.Profiles;
using SaberSense.Rendering;
using System;
using UnityEngine;
using VRUIControls;

namespace SaberSense.GUI.Framework.Menu.Controllers;

internal sealed class PreviewController : IDisposable
{
    private readonly PreviewSession _previewSession;
    private readonly SaberSense.GUI.TrailVisualizationRenderer _trailPreviewer;
    private readonly SaberSense.Customization.SaberEditor _editor;
    private readonly ModSettings _pluginConfig;
    private readonly PlayerDataModel _playerDataModel;
    private readonly SaberSense.Services.CoverGenerationService _coverService;
    private readonly LiveSaber.Factory _liveSaberCreator;
    private readonly SaberSense.Customization.EditScope _editScope;
    private IDisposable? _settingsChangedSub;
    private IDisposable? _configLoadedSub;

    private UISaberPreview? _saberPreview;
    private UILabel? _previewTitleLabel;
    private GameObject? _previewWindowGO;
    private LiveSaber? _mirrorLeft;
    private LiveSaber? _mirrorRight;
    private SaberAssetEntry? _mirrorSourceEntry;
    private readonly GUI.Framework.Core.BindingScope _bindingScope = new();
    private readonly SaberSense.Services.SharedMaterialPool _materialPool;

    private const int PreviewCameraLayer = 12;
    private const int InvisibleLayer = 31;

    private const float AutoSwitchInterval = 15f;
    private RectTransform? _timerFillRect;
    private float _autoSwitchTimer;
    private bool _isAutoMode;
    private UIComboBox? _previewModeCombo;

    public event Action? OnFocusedSaberChanged;

    public PreviewController(
        PreviewSession previewSession,
        SaberSense.GUI.TrailVisualizationRenderer trailVisualizationRenderer,
        SaberSense.Customization.SaberEditor editor,
        ModSettings config,
        PlayerDataModel playerDataModel,
        SaberSense.Services.CoverGenerationService coverService,
        LiveSaber.Factory liveSaberCreator,
        SaberSense.Customization.EditScope editScope,
        SaberSense.Core.Messaging.IMessageBroker broker,
        SaberSense.Services.SharedMaterialPool materialPool)
    {
        _previewSession = previewSession;
        _trailPreviewer = trailVisualizationRenderer;
        _editor = editor;
        _pluginConfig = config;
        _playerDataModel = playerDataModel;
        _coverService = coverService;
        _liveSaberCreator = liveSaberCreator;
        _editScope = editScope;
        _materialPool = materialPool;
        _settingsChangedSub = broker?.Subscribe<SaberSense.Core.Messaging.SettingsChangedMsg>(_ => SyncPreviewMode());
        _configLoadedSub = broker?.Subscribe<SaberSense.Core.Messaging.ConfigLoadedMsg>(_ => OnConfigLoaded());
    }

    public UISaberPreview? SaberPreview => _saberPreview;

    public UILabel? TitleLabel => _previewTitleLabel;

    public GameObject? WindowGO => _previewWindowGO;

    public void BuildPreviewWindow(RectTransform mainCanvasRect,
        PhysicsRaycasterWithCache physicsRaycaster, ModSettings config)
    {
        var (pwRect, pvBg) = BuildWindowFrame(mainCanvasRect, physicsRaycaster);
        var pvContent = BuildContentLayout(pvBg);
        BuildSettingsPanel(pvContent, pwRect, config);
    }

    private (RectTransform pwRect, UIImage pvBg) BuildWindowFrame(
        RectTransform mainCanvasRect, PhysicsRaycasterWithCache physicsRaycaster)
    {
        _previewWindowGO = new GameObject("PreviewWindow");
        var pwRect = _previewWindowGO.AddComponent<RectTransform>();
        pwRect.SetParent(mainCanvasRect, false);
        pwRect.anchorMin = new Vector2(1, 0);
        pwRect.anchorMax = new Vector2(1, 1);
        pwRect.pivot = new Vector2(0, 0.5f);
        pwRect.anchoredPosition = new Vector2(3, 0);
        pwRect.sizeDelta = new Vector2(50, 0);

        var pvCanvas = _previewWindowGO.AddComponent<Canvas>();
        pvCanvas.overrideSorting = true;
        pvCanvas.sortingOrder = 3;
        if (physicsRaycaster != null)
        {
            var vrgr = _previewWindowGO.AddComponent<VRGraphicRaycaster>();
            VRRaycasterHelper.SetPhysicsRaycaster(vrgr, physicsRaycaster);
        }
        _previewWindowGO.AddComponent<CurvedCanvasSettings>().SetRadius(0f);
        pwRect.localEulerAngles = new Vector3(0, 20f, 0);

        var pvBlockerGO = new GameObject("PvRaycastBlocker");
        pvBlockerGO.transform.SetParent(pwRect, false);
        var pvBlockerR = pvBlockerGO.AddComponent<RectTransform>();
        pvBlockerR.anchorMin = Vector2.zero;
        pvBlockerR.anchorMax = Vector2.one;
        pvBlockerR.sizeDelta = Vector2.zero;
        var pvBlockerImg = pvBlockerGO.AddComponent<UnityEngine.UI.Image>();
        pvBlockerImg.color = new Color(0, 0, 0, 0);
        pvBlockerImg.raycastTarget = true;

        byte[] outlineGrays = { 10, 60, 40, 40, 40, 60 };
        float inset = 0f;
        for (int b = 0; b < outlineGrays.Length; b++)
        {
            var borderImg = new UIImage("PvBorder" + b)
                .SetColor(new Color32(outlineGrays[b], outlineGrays[b], outlineGrays[b], 255));
            borderImg.RectTransform.SetParent(pwRect, false);
            borderImg.SetAnchors(Vector2.zero, Vector2.one);
            borderImg.RectTransform.offsetMin = new Vector2(inset, inset);
            borderImg.RectTransform.offsetMax = new Vector2(-inset, -inset);
            borderImg.ImageComponent.raycastTarget = false;
            inset += 0.15f;
        }

        var pvBg = new UIImage("PvBg").SetColor(UITheme.Surface);
        pvBg.RectTransform.SetParent(pwRect, false);
        pvBg.SetAnchors(Vector2.zero, Vector2.one);
        pvBg.RectTransform.offsetMin = new Vector2(inset, inset);
        pvBg.RectTransform.offsetMax = new Vector2(-inset, -inset);
        NavBarBuilder.BuildRainbowBar(pvBg.RectTransform);

        return (pwRect, pvBg);
    }

    private VBox BuildContentLayout(UIImage pvBg)
    {
        var pvContent = new VBox("PvContent").SetParent(pvBg.RectTransform);
        pvContent.SetAnchors(Vector2.zero, Vector2.one);
        pvContent.RectTransform.sizeDelta = Vector2.zero;
        pvContent.RectTransform.anchoredPosition = Vector2.zero;
        UnityEngine.Object.Destroy(pvContent.GameObject.GetComponent<UnityEngine.UI.ContentSizeFitter>());
        pvContent.SetPadding(UITheme.PreviewPad, UITheme.PreviewPad, UITheme.PreviewPad, UITheme.PreviewPad).SetSpacing(UITheme.PreviewSpacing);

        var pvHeader = new HBox("PvHeader").SetParent(pvContent.RectTransform);
        pvHeader.SetSpacing(UITheme.PreviewHeaderSpacing).AddLayoutElement(minHeight: UITheme.HeaderHeight, preferredHeight: UITheme.HeaderHeight, flexibleHeight: 0);
        var headerLayout = pvHeader.GameObject.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (headerLayout != null) headerLayout.childAlignment = TextAnchor.MiddleLeft;

        var pvDot = new UIImage("PvDot").SetColor(UITheme.Accent).SetParent(pvHeader.RectTransform)
            .AddLayoutElement(preferredWidth: UITheme.AccentBarWidth, preferredHeight: UITheme.LabelHeight);
        UITheme.TrackAccent(pvDot.ImageComponent);

        _previewTitleLabel = new UILabel("PvTitle", "PREVIEW").SetFontSize(UITheme.FontNormal).SetColor(UITheme.TextPrimary)
            .SetAlignment(TMPro.TextAlignmentOptions.MidlineLeft);
        _previewTitleLabel.TextComponent.enableAutoSizing = true;
        _previewTitleLabel.TextComponent.fontSizeMin = 2.0f;
        _previewTitleLabel.TextComponent.overflowMode = TMPro.TextOverflowModes.Overflow;
        _previewTitleLabel.SetParent(pvHeader.RectTransform).AddLayoutElement(flexibleWidth: 1);

        new UIImage("PvHdrSep").SetColor(UITheme.Divider)
            .SetParent(pvContent.RectTransform).AddLayoutElement(preferredHeight: UITheme.SeparatorHeight, flexibleWidth: 1);

        _saberPreview = new UISaberPreview("SaberPv");
        _coverService?.SetCaptureSource(size => _saberPreview?.CaptureSnapshot(size)!);
        _saberPreview.SetParent(pvContent.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var timerBarContainer = new UIImage("PvTimerBg").SetColor(UITheme.Divider);
        timerBarContainer.SetParent(pvContent.RectTransform).AddLayoutElement(preferredHeight: UITheme.SeparatorHeight, flexibleWidth: 1);

        var timerFill = new UIImage("PvTimerFill").SetColor(UITheme.Accent);
        UITheme.TrackAccent(timerFill.ImageComponent);
        _timerFillRect = timerFill.RectTransform;
        _timerFillRect.SetParent(timerBarContainer.RectTransform, false);
        _timerFillRect.anchorMin = Vector2.zero;
        _timerFillRect.anchorMax = Vector2.zero;
        _timerFillRect.offsetMin = Vector2.zero;
        _timerFillRect.offsetMax = Vector2.zero;

        new UIImage("PvSepSpacer").SetColor(Color.clear).SetParent(pvContent.RectTransform).AddLayoutElement(preferredHeight: 0.5f, flexibleWidth: 1);

        return pvContent;
    }

    private void BuildSettingsPanel(VBox pvContent, RectTransform pwRect, ModSettings config)
    {
        var pvSettingsPanel = new UIGroupBox("Settings");
        pvSettingsPanel.SetParent(pvContent.RectTransform).AddLayoutElement(flexibleWidth: 1);

        var bloomToggle = new UIToggle().Bind(config, c => c.Editor.Bloom, val => { _saberPreview?.SetBloom(val); }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Bloom", bloomToggle, pvSettingsPanel.Content, experimental: true);
        _saberPreview?.SetBloom(config.Editor.Bloom);

        var trailToggle = new UIToggle().Bind(config, c => c.Editor.DisplayTrails, val =>
        {
            _saberPreview?.SetDisplayTrails(val);
            SetTrailVisualizerVisible(val);
        }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Display trails", trailToggle, pvSettingsPanel.Content);
        _saberPreview?.SetDisplayTrails(config.Editor.DisplayTrails);
        SetTrailVisualizerVisible(config.Editor.DisplayTrails);

        var rotSpeedSlider = new UISlider().SetRange(-100, 100).Bind(config, c => c.Editor.RotationSpeed, val =>
        {
            _saberPreview?.SetRotation(config.Editor.Rotation, val);
        }, scope: _bindingScope);
        rotSpeedSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");

        var rotToggle = new UIToggle().Bind(config, c => c.Editor.Rotation, val =>
        {
            _saberPreview?.SetRotation(val, config.Editor.RotationSpeed);
        }, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Rotation", rotToggle, pvSettingsPanel.Content);
        var rotSpeedRow = UILayoutFactory.SliderRow("Rotation amount", rotSpeedSlider, pvSettingsPanel.Content);
        rotToggle.ControlsVisibility(rotSpeedRow);
        _saberPreview?.SetRotation(config.Editor.Rotation, config.Editor.RotationSpeed);

        _previewModeCombo = new UIComboBox("PreviewModeCombo", pwRect)
            .SetOptions(["Automatic", "Left saber", "Right saber"])
            .SetSelected(config.Editor.SaberPreviewMode)
            .SetOpenUpward()
            .OnSelect((idx, _) =>
            {
                config.Editor.SaberPreviewMode = idx;
                _isAutoMode = idx == 0;
                _autoSwitchTimer = 0f;
                if (idx == 1)
                {
                    _previewSession.FocusedHand = SaberHand.Left;
                    ShowFocusedSaber();
                }
                else if (idx == 2)
                {
                    _previewSession.FocusedHand = SaberHand.Right;
                    ShowFocusedSaber();
                }
                UpdateTimerBar();
            });
        UILayoutFactory.DropdownRow("Saber preview", _previewModeCombo, pvSettingsPanel.Content);

        _isAutoMode = config.Editor.SaberPreviewMode == 0;
        if (config.Editor.SaberPreviewMode == 1)
            _previewSession.FocusedHand = SaberHand.Left;
        else if (config.Editor.SaberPreviewMode == 2)
            _previewSession.FocusedHand = SaberHand.Right;
        UpdateTimerBar();

        pvSettingsPanel.SizeToContent();
        pvSettingsPanel.GameObject.SetActive(false);

        var pvSettingsBtn = new BaseButton("Preview settings");
        pvSettingsBtn.SetParent(pvContent.RectTransform).AddLayoutElement(flexibleWidth: 1, minHeight: UITheme.ActionRowHeight, preferredHeight: UITheme.ActionRowHeight);
        pvSettingsBtn.OnClick = () =>
        {
            bool expanding = !pvSettingsPanel.GameObject.activeSelf;
            pvSettingsPanel.GameObject.SetActive(expanding);
            pvSettingsBtn.SetText(expanding ? "Close settings" : "Preview settings");
        };
    }

    public void CreateTrailPreview(LiveSaber? displaySaber = null, LiveSaber? trailDataSource = null)
    {
        _trailPreviewer?.Destroy();
        var hand = _previewSession.FocusedHand;
        var liveSaber = displaySaber ?? _previewSession.FocusedSaber;
        var dataSource = trailDataSource ?? liveSaber;
        var trailData = dataSource?.GetTrailLayout().Primary;
        if (trailData == null) return;

        if (liveSaber!.TrailHandler == null)
        {
            _trailPreviewer!.Create(
                liveSaber.GameObject.transform.parent,
                trailData,
                _pluginConfig?.Trail?.VertexColorOnly ?? true
            );

            _trailPreviewer.SetLayer(PreviewCameraLayer);

            try
            {
                var scheme = _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme();
                _trailPreviewer.SetColor(hand == SaberHand.Left ? scheme.saberAColor : scheme.saberBColor);
            }
            catch (Exception ex) { ModLogger.ForSource("PreviewController").Debug($"PlayerDataModel unavailable: {ex.Message}"); }
        }

        SetTrailVisualizerVisible(_pluginConfig?.Editor?.DisplayTrails ?? true);
    }

    public void OnSaberPreviewInstantiated(LiveSaber saber, SaberHand hand)
    {
        bool anyGrabbed = (_editor is not null && (_editor.GrabLeft || _editor.GrabRight));

        if (anyGrabbed)
        {
            ClearMirror();

            var sabers = _previewSession?.Sabers;
            if (sabers?.Left != null)
            {
                _mirrorLeft = _liveSaberCreator.Create(sabers.Left.Profile);
                _mirrorLeft.CachedTransform.position = new Vector3(0, -1000, 0);
                _mirrorLeft.SetColor(GetHandColor(SaberHand.Left));
            }
            if (sabers?.Right != null)
            {
                _mirrorRight = _liveSaberCreator.Create(sabers.Right.Profile);
                _mirrorRight.CachedTransform.position = new Vector3(0, -1000, 0);
                _mirrorRight.SetColor(GetHandColor(SaberHand.Right));
            }
            _mirrorSourceEntry = _previewSession?.ActiveEntry;

            var activeMirror = MirrorFor(hand);
            var inactiveMirror = InactiveMirrorFor(hand);
            inactiveMirror?.GameObject?.SetActive(false);

            _editScope.PreviewMirror = activeMirror;
            _saberPreview?.SetSaber(activeMirror!);
        }
        else
        {
            ClearMirror();

            _saberPreview?.SetSaber(saber);
        }

        CreateTrailPreview(MirrorFor(hand));
    }

    private void ShowFocusedSaber()
    {
        var sabers = _previewSession?.Sabers;
        if (sabers is null) return;

        var hand = _previewSession!.FocusedHand;
        var targetSaber = sabers[hand];
        if (targetSaber == null) return;

        bool isGrabbed = hand == SaberHand.Left
            ? (_editor is not null && _editor.GrabLeft)
            : (_editor is not null && _editor.GrabRight);

        if (isGrabbed)
        {
            var activeMirror = MirrorFor(hand);
            var inactiveMirror = InactiveMirrorFor(hand);

            if (activeMirror?.GameObject != null)
            {
                activeMirror.GameObject.SetActive(true);
                inactiveMirror?.GameObject?.SetActive(false);

                _editScope.PreviewMirror = activeMirror;
                _saberPreview?.SetSaber(activeMirror);
                _editor?.UpdateSaberVisibility();
                _previewSession.RefreshActiveRenderer();

                activeMirror.DestroyTrail(true);

                CreateTrailPreview(activeMirror);
                OnFocusedSaberChanged?.Invoke();
                return;
            }

            ClearMirror();
            var newMirror = _liveSaberCreator.Create(targetSaber.Profile);
            newMirror.CachedTransform.position = new Vector3(0, -1000, 0);
            newMirror.SetColor(GetHandColor(hand));
            if (hand == SaberHand.Left) _mirrorLeft = newMirror;
            else _mirrorRight = newMirror;
            _editScope.PreviewMirror = newMirror;
            _saberPreview?.SetSaber(newMirror);
        }
        else
        {
            ClearMirror();
            _saberPreview?.SetSaber(targetSaber);
        }

        CreateTrailPreview(MirrorFor(hand));

        _editor?.UpdateSaberVisibility();

        _previewSession.RefreshActiveRenderer();
        OnFocusedSaberChanged?.Invoke();
    }

    public void Tick()
    {
        if (!_isAutoMode || _previewSession?.Sabers == null) return;

        if (_previewSession.FocusedSaber == null) return;

        if (_saberPreview != null && _saberPreview.IsDragging)
        {
            UpdateTimerBar();
            return;
        }

        _autoSwitchTimer += Time.deltaTime;
        UpdateTimerBar();

        if (_autoSwitchTimer >= AutoSwitchInterval)
        {
            _autoSwitchTimer = 0f;
            _previewSession.FocusedHand = _previewSession.FocusedHand == SaberHand.Left
                ? SaberHand.Right
                : SaberHand.Left;
            ShowFocusedSaber();
        }
    }

    private void SyncPreviewMode()
    {
        if (_pluginConfig?.Editor is null) return;
        var ed = _pluginConfig.Editor;

        _saberPreview?.SetBloom(ed.Bloom);
        _saberPreview?.SetDisplayTrails(ed.DisplayTrails);
        SetTrailVisualizerVisible(ed.DisplayTrails);
        _saberPreview?.SetRotation(ed.Rotation, ed.RotationSpeed);

        int mode = ed.SaberPreviewMode;
        _isAutoMode = mode == 0;
        _autoSwitchTimer = 0f;
        _previewModeCombo?.SetSelected(mode);
        if (mode == 1)
            _previewSession.FocusedHand = SaberHand.Left;
        else if (mode == 2)
            _previewSession.FocusedHand = SaberHand.Right;
        UpdateTimerBar();
    }

    private void UpdateTimerBar()
    {
        if (_timerFillRect == null) return;

        if (_isAutoMode && _previewSession?.FocusedSaber != null)
        {
            float fill = Mathf.Clamp01(_autoSwitchTimer / AutoSwitchInterval);
            _timerFillRect.anchorMax = new Vector2(fill, 1f);
        }
        else
        {
            _timerFillRect.anchorMax = new Vector2(0f, 1f);
        }
    }

    public void SetTitle(string text)
    {
        _previewTitleLabel?.SetText(text);
    }

    public UnityEngine.Sprite? CaptureSnapshot(int size = 128) => _saberPreview?.CaptureSnapshot(size);

    public void Cleanup()
    {
        _coverService?.ClearCaptureSource();
        ClearMirror();

        _saberPreview?.Dispose();
        _saberPreview = null;

        if (_previewWindowGO != null)
            UnityEngine.Object.Destroy(_previewWindowGO);
        _previewWindowGO = null;
    }

    private void SetTrailVisualizerVisible(bool val)
    {
        if (_trailPreviewer is null) return;

        _trailPreviewer.SetLayer(val ? PreviewCameraLayer : InvisibleLayer);
    }

    private void OnConfigLoaded()
    {
        ClearMirror();
    }

    private void ClearMirror()
    {
        _mirrorLeft?.Destroy();
        _mirrorLeft = null;
        _mirrorRight?.Destroy();
        _mirrorRight = null;
        _mirrorSourceEntry = null;
        _editScope.PreviewMirror = null;
    }

    private LiveSaber? MirrorFor(SaberHand hand) => hand == SaberHand.Left ? _mirrorLeft : _mirrorRight;
    private LiveSaber? InactiveMirrorFor(SaberHand hand) => hand == SaberHand.Left ? _mirrorRight : _mirrorLeft;

    private Color GetHandColor(SaberHand hand)
    {
        try
        {
            var scheme = _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme();
            return hand == SaberHand.Left ? scheme.saberAColor : scheme.saberBColor;
        }
        catch { return Color.white; }
    }

    public void Dispose()
    {
        _settingsChangedSub?.Dispose();
        _settingsChangedSub = null;
        _configLoadedSub?.Dispose();
        _configLoadedSub = null;
        _bindingScope.Dispose();
        Cleanup();
    }
}