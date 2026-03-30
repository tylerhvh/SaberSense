// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HMUI;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Framework.Menu.Tabs;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace SaberSense.GUI.Framework.Menu.Controllers;

internal sealed class LogConsoleController : IDisposable
{
    private readonly LogRingBuffer _ringBuffer;
    private readonly LogFileWriter _fileWriter;
    private readonly IMessageBroker _broker;

    private UILogConsole? _console;
    private GameObject? _windowGO;
    private IDisposable? _logSub;

    private bool _showDebug = true;
    private bool _showInfo = true;
    private bool _showWarn = true;
    private bool _showError = true;

    public LogConsoleController(LogRingBuffer ringBuffer, LogFileWriter fileWriter, IMessageBroker broker)
    {
        _ringBuffer = ringBuffer;
        _fileWriter = fileWriter;
        _broker = broker;
    }

    public GameObject? WindowGO => _windowGO;

    public void BuildConsoleWindow(RectTransform mainCanvasRect,
        PhysicsRaycasterWithCache physicsRaycaster)
    {
        _windowGO = new GameObject("ConsoleWindow");
        var cwRect = _windowGO.AddComponent<RectTransform>();
        cwRect.SetParent(mainCanvasRect, false);
        cwRect.anchorMin = new Vector2(0, 0);
        cwRect.anchorMax = new Vector2(0, 1);
        cwRect.pivot = new Vector2(1, 0.5f);
        cwRect.anchoredPosition = new Vector2(-3, 0);
        cwRect.sizeDelta = new Vector2(50, 0);

        var cwCanvas = _windowGO.AddComponent<Canvas>();
        cwCanvas.overrideSorting = true;
        cwCanvas.sortingOrder = 3;
        if (physicsRaycaster != null)
        {
            var vrgr = _windowGO.AddComponent<VRGraphicRaycaster>();
            VRRaycasterHelper.SetPhysicsRaycaster(vrgr, physicsRaycaster);
        }
        _windowGO.AddComponent<CurvedCanvasSettings>().SetRadius(0f);

        cwRect.localEulerAngles = new Vector3(0, -20f, 0);

        var blockerGO = new GameObject("CwRaycastBlocker");
        blockerGO.transform.SetParent(cwRect, false);
        var blockerR = blockerGO.AddComponent<RectTransform>();
        blockerR.anchorMin = Vector2.zero;
        blockerR.anchorMax = Vector2.one;
        blockerR.sizeDelta = Vector2.zero;
        var blockerImg = blockerGO.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0);
        blockerImg.raycastTarget = true;

        byte[] outlineGrays = { 10, 60, 40, 40, 40, 60 };
        float inset = 0f;
        for (int b = 0; b < outlineGrays.Length; b++)
        {
            var borderImg = new UIImage("CwBorder" + b)
                .SetColor(new Color32(outlineGrays[b], outlineGrays[b], outlineGrays[b], 255));
            borderImg.RectTransform.SetParent(cwRect, false);
            borderImg.SetAnchors(Vector2.zero, Vector2.one);
            borderImg.RectTransform.offsetMin = new Vector2(inset, inset);
            borderImg.RectTransform.offsetMax = new Vector2(-inset, -inset);
            borderImg.ImageComponent.raycastTarget = false;
            inset += 0.15f;
        }

        var cwBg = new UIImage("CwBg").SetColor(UITheme.Surface);
        cwBg.RectTransform.SetParent(cwRect, false);
        cwBg.SetAnchors(Vector2.zero, Vector2.one);
        cwBg.RectTransform.offsetMin = new Vector2(inset, inset);
        cwBg.RectTransform.offsetMax = new Vector2(-inset, -inset);

        NavBarBuilder.BuildRainbowBar(cwBg.RectTransform);

        var cwContent = new VBox("CwContent").SetParent(cwBg.RectTransform);
        cwContent.SetAnchors(Vector2.zero, Vector2.one);
        cwContent.RectTransform.sizeDelta = Vector2.zero;
        cwContent.RectTransform.anchoredPosition = Vector2.zero;
        UnityEngine.Object.Destroy(cwContent.GameObject.GetComponent<ContentSizeFitter>());
        cwContent.SetPadding(UITheme.PreviewPad, UITheme.PreviewPad, UITheme.PreviewPad, UITheme.PreviewPad).SetSpacing(UITheme.PreviewSpacing);

        var cwHeader = new HBox("CwHeader").SetParent(cwContent.RectTransform);
        cwHeader.SetSpacing(UITheme.PreviewHeaderSpacing)
            .AddLayoutElement(minHeight: UITheme.HeaderHeight, preferredHeight: UITheme.HeaderHeight, flexibleHeight: 0);

        var headerLayout = cwHeader.GameObject.GetComponent<HorizontalLayoutGroup>();
        if (headerLayout != null) headerLayout.childAlignment = TextAnchor.MiddleLeft;

        var cwDot = new UIImage("CwDot").SetColor(UITheme.Accent).SetParent(cwHeader.RectTransform)
            .AddLayoutElement(preferredWidth: UITheme.AccentBarWidth, preferredHeight: UITheme.LabelHeight);
        UITheme.TrackAccent(cwDot.ImageComponent);

        new UILabel("CwTitle", "CONSOLE").SetFontSize(UITheme.FontNormal).SetColor(UITheme.TextPrimary)
            .SetAlignment(TMPro.TextAlignmentOptions.MidlineLeft)
            .SetParent(cwHeader.RectTransform).AddLayoutElement(flexibleWidth: 1);

        new UIImage("CwHdrSep").SetColor(UITheme.Divider)
            .SetParent(cwContent.RectTransform).AddLayoutElement(preferredHeight: UITheme.SeparatorHeight, flexibleWidth: 1);

        _console = new UILogConsole("ConsolePv");
        _console.SetParent(cwContent.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        new UIImage("CwBtnSep").SetColor(UITheme.Divider)
            .SetParent(cwContent.RectTransform).AddLayoutElement(preferredHeight: UITheme.SeparatorHeight, flexibleWidth: 1);

        new UIImage("CwSepSpacer").SetColor(Color.clear)
            .SetParent(cwContent.RectTransform).AddLayoutElement(preferredHeight: 0.5f, flexibleWidth: 1, flexibleHeight: 0);

        var cwSettingsPanel = new UIGroupBox("Filters");
        cwSettingsPanel.SetParent(cwContent.RectTransform).AddLayoutElement(flexibleWidth: 1);

        BuildFilterToggle("Debug", true, val => { _showDebug = val; RebuildFiltered(); }, cwSettingsPanel.Content);
        BuildFilterToggle("Info", true, val => { _showInfo = val; RebuildFiltered(); }, cwSettingsPanel.Content);
        BuildFilterToggle("Warn", true, val => { _showWarn = val; RebuildFiltered(); }, cwSettingsPanel.Content);
        BuildFilterToggle("Error", true, val => { _showError = val; RebuildFiltered(); }, cwSettingsPanel.Content);

        cwSettingsPanel.SizeToContent();
        cwSettingsPanel.GameObject.SetActive(false);

        var cwSettingsBtn = new BaseButton("Console settings");
        cwSettingsBtn.SetParent(cwContent.RectTransform)
            .AddLayoutElement(flexibleWidth: 1, minHeight: UITheme.ActionRowHeight, preferredHeight: UITheme.ActionRowHeight, flexibleHeight: 0);
        cwSettingsBtn.OnClick = () =>
        {
            bool expanding = !cwSettingsPanel.GameObject.activeSelf;
            cwSettingsPanel.GameObject.SetActive(expanding);
            cwSettingsBtn.SetText(expanding ? "Close settings" : "Console settings");
        };

        var actionRow = new HBox("CwActions").SetParent(cwContent.RectTransform);
        actionRow.SetSpacing(1f).AddLayoutElement(flexibleWidth: 1, minHeight: UITheme.ActionRowHeight, preferredHeight: UITheme.ActionRowHeight, flexibleHeight: 0);

        var copyBtn = new BaseButton("Copy to clipboard");
        copyBtn.SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        copyBtn.OnClick = () =>
        {
            try
            {
                using var fs = new FileStream(_fileWriter.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, System.Text.Encoding.UTF8);
                ClipboardHelper.SetText(sr.ReadToEnd());
            }
            catch (Exception ex)
            {
                ModLogger.ForSource("LogConsole").Warn($"Clipboard copy failed: {ex.Message}");
            }
        };

        var clearBtn = new BaseButton("Clear");
        clearBtn.SetParent(actionRow.RectTransform).AddLayoutElement(preferredWidth: 12);
        clearBtn.OnClick = () =>
        {
            _ringBuffer.Clear();
            _console.Clear();
        };

        _logSub = _broker?.Subscribe<LogEntryMsg>(msg => _console.AppendEntry(msg.Entry));

        RebuildFiltered();
    }

    private void RebuildFiltered()
    {
        if (_console is null) return;

        var entries = _ringBuffer.GetEntries(LogLevel.Debug);

        if (!_showDebug || !_showInfo || !_showWarn || !_showError)
        {
            entries.RemoveAll(e =>
                (e.Level == LogLevel.Debug && !_showDebug) ||
                (e.Level == LogLevel.Info && !_showInfo) ||
                (e.Level == LogLevel.Warn && !_showWarn) ||
                (e.Level == LogLevel.Error && !_showError));
        }

        _console.Rebuild(entries);
    }

    private static void BuildFilterToggle(string label, bool defaultVal,
        Action<bool> onChange, RectTransform parent)
    {
        var toggle = new UIToggle(defaultValue: defaultVal);
        toggle.OnValueChanged(onChange);
        UILayoutFactory.CheckboxRow(label, toggle, parent);
    }

    public void Dispose()
    {
        _logSub?.Dispose();
        _logSub = null;

        _console?.Dispose();
        _console = null;

        if (_windowGO != null)
            UnityEngine.Object.Destroy(_windowGO);
        _windowGO = null;
    }
}