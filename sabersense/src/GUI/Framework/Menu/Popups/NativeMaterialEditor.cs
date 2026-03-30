// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.GUI.Framework.Core;
using SaberSense.Rendering.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.GUI.Framework.Menu;

internal sealed class NativeMaterialEditor
{
    private const float ModalWidth = 90f;
    private const float ModalHeight = 70f;

    private readonly UIModal _modal;
    private readonly UIGroupBox _groupBox;
    private readonly ShaderIntrospector _shaderCache;
    private readonly RectTransform _canvasRoot;
    private readonly NativeTexturePicker? _texturePicker;
    private readonly Services.OriginalMaterialCache? _originalCache;
    private readonly IModLogger? _logger;

    private Action? _onClose;
    private Action<Material>? _onPropertyChanged;
    private Action<Material>? _onCommit;

    private Material? _activeMaterial;
    private string? _activeMaterialName;
    private Dictionary<int, object> _snapshot = [];

    public bool IsOpen => _modal.Backdrop.GameObject.activeSelf;

    public NativeMaterialEditor(RectTransform canvasRoot, ShaderIntrospector shaderCache, TextureCache textureStore, Services.OriginalMaterialCache? originalCache = null, IModLogger? logger = null, Action? onClose = null, Action<Material>? onPropertyChanged = null, Action<Material>? onCommit = null)
    {
        _shaderCache = shaderCache;
        _canvasRoot = canvasRoot;
        _originalCache = originalCache;
        _logger = logger;
        _onClose = onClose;
        _onPropertyChanged = onPropertyChanged;
        _onCommit = onCommit;
        _modal = new UIModal("Material editor", canvasRoot, ModalWidth, ModalHeight);
        if (textureStore is not null)
            _texturePicker = new NativeTexturePicker(canvasRoot, textureStore);

        _groupBox = new UIGroupBox("Shader properties");
        _groupBox.RectTransform.SetParent(_modal.ContentArea.RectTransform, false);
        _groupBox.AddLayoutElement(flexibleHeight: 1, flexibleWidth: 1);

        _modal.AddButtons("Close", () => { _modal.Hide(); _onClose?.Invoke(); },
                          "Cancel", () => { RestoreSnapshot(); _modal.Hide(); _onClose?.Invoke(); });

        _modal.ButtonsRow!.LayoutGroup!.childAlignment = TextAnchor.MiddleCenter;
        _modal.ButtonsRow!.LayoutGroup!.childForceExpandWidth = true;
    }

    private void SnapshotMaterial(Material material, IReadOnlyList<ShaderProperty> shaderProps)
    {
        _snapshot.Clear();
        _activeMaterial = material;
        foreach (var prop in shaderProps)
        {
            object? value = prop.Kind switch
            {
                PropertyKind.Color => material.GetColor(prop.Id),
                PropertyKind.Texture => material.GetTexture(prop.Id),
                PropertyKind.Float or PropertyKind.Range => material.GetFloat(prop.Id),
                _ => null
            };
            if (value is not null) _snapshot[prop.Id] = value;
        }
    }

    private void RestoreSnapshot()
    {
        if (_activeMaterial == null) return;
        foreach (var kvp in _snapshot)
        {
            if (kvp.Value is Color c)
                _activeMaterial.SetColor(kvp.Key, c);
            else if (kvp.Value is Texture t)
                _activeMaterial.SetTexture(kvp.Key, t);
            else if (kvp.Value is float f)
                _activeMaterial.SetFloat(kvp.Key, f);
        }
        _onPropertyChanged?.Invoke(_activeMaterial);

        _onCommit?.Invoke(_activeMaterial);
    }

    public void Show(SaberSense.Rendering.MaterialHandle MaterialHandle, string? materialName = null)
    {
        if (MaterialHandle is null || MaterialHandle.Material == null) return;
        var material = MaterialHandle.Material;
        _activeMaterialName = materialName ?? SaberSense.Core.Utilities.MaterialNameResolver.StripInstanceSuffix(material.name);
        foreach (Transform child in _groupBox.Content)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        var shaderProps = _shaderCache[material.shader]!;
        SnapshotMaterial(material, shaderProps);

        foreach (var prop in shaderProps)
        {
            if (prop.Attributes.Contains("MaterialToggle") || prop.Name == "_CustomColors")
            {
                BuildToggleRow(material, prop);
            }
            else if (prop.Kind == PropertyKind.Range)
            {
                BuildSliderRow(material, prop, prop.RangeMin ?? 0, prop.RangeMax ?? 1);
            }
            else if (prop.Kind == PropertyKind.Float)
            {
                BuildSliderRow(material, prop, 0, 10);
            }
            else if (prop.Kind == PropertyKind.Color)
            {
                BuildColorRow(material, prop);
            }
            else if (prop.Kind == PropertyKind.Texture)
            {
                BuildTextureRow(material, prop);
            }
        }

        _modal.Show();
    }

    public void Close()
    {
        _modal.Hide();
        _texturePicker?.Exit();
    }

    private void BuildToggleRow(Material material, ShaderProperty prop)
    {
        bool val = ((float)prop.ReadFrom(material)!) > 0;
        var toggle = new UIToggle().SetValue(val);
        toggle.OnValueChanged(v => { material.SetFloat(prop.Id, v ? 1 : 0); _onPropertyChanged?.Invoke(material); });

        var row = new HBox(prop.Description + "CR");
        row.RectTransform.SetParent(_groupBox.Content, false);
        row.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        row.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        row.LayoutGroup.childForceExpandHeight = false;
        toggle.SetParent(row.RectTransform);
        new UILabel(prop.Description + "L", "  " + prop.Description)
            .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
            .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform)
            .AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        UILayoutFactory.AddRowHitArea(row.RectTransform, toggle);
    }

    private void BuildSliderRow(Material material, ShaderProperty prop, float min, float max)
    {
        var val = material.GetFloat(prop.Id);
        var slider = new UISlider().SetRange(min, max).SetValue(val);
        slider.OnValueChanged(v => { material.SetFloat(prop.Id, v); _onPropertyChanged?.Invoke(material); });
        slider.OnCommit(v => _onCommit?.Invoke(material));

        var row = new VBox("SliderRow");
        row.RectTransform.SetParent(_groupBox.Content, false);
        row.AddLayoutElement(preferredHeight: UITheme.SliderRowHeight);
        row.SetSpacing(UITheme.RowInnerSpacing).SetPadding(0, 0, 0, 0);
        row.LayoutGroup.childForceExpandWidth = true;

        var label = new UILabel("Label", prop.Description)
            .SetFontSize(UITheme.FontSmall)
            .SetColor(UITheme.TextLabel)
            .SetAlignment(TMPro.TextAlignmentOptions.Left);
        label.RectTransform.SetParent(row.RectTransform, false);
        label.AddLayoutElement(preferredHeight: UITheme.LabelHeight);

        slider.RectTransform.SetParent(row.RectTransform, false);
        slider.AddLayoutElement(preferredHeight: 1f, flexibleWidth: 1);
    }

    private void BuildColorRow(Material material, ShaderProperty prop)
    {
        var color = material.GetColor(prop.Id);
        var colorPicker = new UIColorPicker("ColorPicker", _canvasRoot)
            .SetColor(color)
            .OnColorChanged(c => { material.SetColor(prop.Id, c); _onPropertyChanged?.Invoke(material); })
            .OnCommit(c => _onCommit?.Invoke(material));
        colorPicker.SetResetColor(color);

        var row = new UIPropRow(prop.Description, colorPicker, 10);
        row.RectTransform.SetParent(_groupBox.Content, false);
        row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);
    }

    private void BuildTextureRow(Material material, ShaderProperty prop)
    {
        var currentTex = material.GetTexture(prop.Id);
        var texName = FormatTextureName(currentTex);

        var btn = new BaseButton(texName);
        var row = new UIPropRow(prop.Description + " (Texture)", btn, 30);
        row.RectTransform.SetParent(_groupBox.Content, false);
        row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);

        var matName = _activeMaterialName;
        var propName = prop.Name;
        var snapPropId = prop.Id;
        btn.OnClick = () =>
        {
            var originalTex = (_originalCache?.GetOriginalTexture(matName!, propName) as Texture2D);

            if (originalTex == null && _snapshot.TryGetValue(snapPropId, out var snapped) && snapped is Texture2D snapTex)
                originalTex = snapTex;
            if (_texturePicker is not null) ErrorBoundary.FireAndForget(_texturePicker.ShowAsync((Texture2D? newTex) =>
            {
                material.SetTexture(prop.Id, newTex);
                if (newTex != null) btn.Label.SetText(FormatTextureName(newTex));
                _onPropertyChanged?.Invoke(material);
            }, originalTex), _logger!, "TexturePicker");
        };
    }

    private static string FormatTextureName(Texture tex)
    {
        if (tex == null) return "None";
        var name = tex.name;
        if (string.IsNullOrEmpty(name)) return "None";
        if (name.Length > 15) name = name[..15] + "...";
        return name;
    }
}