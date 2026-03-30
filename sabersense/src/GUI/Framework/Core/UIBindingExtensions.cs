// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

internal static class UIBindingExtensions
{
    public static UIToggle Bind<TSource>(this UIToggle toggle, TSource config,
        Expression<Func<TSource, bool>> property,
        Action<bool>? onChanged = null, BindingScope? scope = null)
        where TSource : BindableSettings
    {
        var accessor = PropertyAccessor.FromExpression(config, property);
        if (accessor is null) return toggle;

        toggle.SetValue((bool)accessor.GetValue()!);
        toggle.OnValueChanged(val =>
        {
            accessor.SetValue(val);
            onChanged?.Invoke(val);
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                accessor.PropertyPath == e.PropertyName ||
                accessor.PropertyPath.StartsWith(e.PropertyName + "."))
            {
                var val = (bool)accessor.GetValue()!;
                toggle.SetValue(val);
            }
        };
        RegisterHandler(config, handler, scope);

        return toggle;
    }

    public static UISlider Bind<TSource>(this UISlider slider, TSource config,
        Expression<Func<TSource, float>> property,
        Action<float>? onChanged = null, BindingScope? scope = null)
        where TSource : BindableSettings
    {
        var accessor = PropertyAccessor.FromExpression(config, property);
        if (accessor is null) return slider;

        slider.SetValue((float)accessor.GetValue()!);
        slider.OnValueChanged(val =>
        {
            accessor.SetValue(val);
            onChanged?.Invoke(val);
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                accessor.PropertyPath == e.PropertyName ||
                accessor.PropertyPath.StartsWith(e.PropertyName + "."))
            {
                var val = (float)accessor.GetValue()!;
                slider.SetValue(val);
            }
        };
        RegisterHandler(config, handler, scope);

        return slider;
    }

    public static UISlider BindInt<TSource>(this UISlider slider, TSource config,
        Expression<Func<TSource, int>> property,
        Action<int>? onChanged = null, BindingScope? scope = null)
        where TSource : BindableSettings
    {
        var accessor = PropertyAccessor.FromExpression(config, property);
        if (accessor is null) return slider;

        slider.SetValue((int)accessor.GetValue()!);
        slider.OnValueChanged(val =>
        {
            accessor.SetValue(Mathf.RoundToInt(val));
            onChanged?.Invoke(Mathf.RoundToInt(val));
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                accessor.PropertyPath == e.PropertyName ||
                accessor.PropertyPath.StartsWith(e.PropertyName + "."))
            {
                var val = (int)accessor.GetValue()!;
                slider.SetValue(val);
            }
        };
        RegisterHandler(config, handler, scope);

        return slider;
    }

    public static UIMultiComboBox BindList<TSource>(this UIMultiComboBox combo, TSource config,
        Expression<Func<TSource, List<int>>> property,
        Action<HashSet<int>>? onChanged = null, BindingScope? scope = null)
        where TSource : BindableSettings
    {
        var accessor = PropertyAccessor.FromExpression(config, property);
        if (accessor is null) return combo;

        var existingList = accessor.GetValue() as List<int>;
        if (existingList is not null && existingList.Count is > 0)
        {
            combo.SetSelected(existingList);
            onChanged?.Invoke([.. existingList]);
        }

        combo.OnSelectionChanged(val =>
        {
            accessor.SetValue(new List<int>(val));
            onChanged?.Invoke(val);
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                accessor.PropertyPath == e.PropertyName ||
                accessor.PropertyPath.StartsWith(e.PropertyName + "."))
            {
                var list = accessor.GetValue() as List<int>;
                combo.SetSelected(list ?? []);
            }
        };
        RegisterHandler(config, handler, scope);

        return combo;
    }

    public static UIComboBox BindInt<TSource>(this UIComboBox combo, TSource config,
        Expression<Func<TSource, int>> property,
        Action<int>? onChanged = null, BindingScope? scope = null)
        where TSource : BindableSettings
    {
        var accessor = PropertyAccessor.FromExpression(config, property);
        if (accessor is null) return combo;

        combo.SetSelected((int)accessor.GetValue()!);
        combo.OnSelect((idx, text) =>
        {
            accessor.SetValue(idx);
            onChanged?.Invoke(idx);
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                accessor.PropertyPath == e.PropertyName ||
                accessor.PropertyPath.StartsWith(e.PropertyName + "."))
            {
                var val = (int)accessor.GetValue()!;
                combo.SetSelected(val);
            }
        };
        RegisterHandler(config, handler, scope);

        return combo;
    }

    public static UIKeybindButton BindInt<TSource>(this UIKeybindButton keybind, TSource config,
        Expression<Func<TSource, int>> property,
        Action<int>? onChanged = null, BindingScope? scope = null)
        where TSource : BindableSettings
    {
        var accessor = PropertyAccessor.FromExpression(config, property);
        if (accessor is null) return keybind;

        keybind.SetValue((int)accessor.GetValue()!);
        keybind.OnValueChanged(val =>
        {
            accessor.SetValue(val);
            onChanged?.Invoke(val);
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                accessor.PropertyPath == e.PropertyName ||
                accessor.PropertyPath.StartsWith(e.PropertyName + "."))
            {
                var val = (int)accessor.GetValue()!;
                keybind.SetValue(val);
            }
        };
        RegisterHandler(config, handler, scope);

        return keybind;
    }

    public static UIColorPicker BindColor(this UIColorPicker picker,
        ModSettings config,
        Func<Color> getter,
        Action<Color> setter,
        string ownerPropertyPath,
        Action<Color>? onLiveChange = null,
        BindingScope? scope = null)
    {
        if (config is null || picker is null) return picker!;

        picker.SetColor(getter());

        if (onLiveChange is not null)
            picker.OnColorChanged(onLiveChange);

        picker.OnCommit(c =>
        {
            setter(c);
            config.RaisePropertyChanged(ownerPropertyPath);
        });

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == ownerPropertyPath ||
                ownerPropertyPath.StartsWith(e.PropertyName + "."))
            {
                picker.SetColor(getter());
            }
        };
        RegisterHandler(config, handler, scope);

        return picker;
    }

    private static void RegisterHandler(INotifyPropertyChanged config,
        PropertyChangedEventHandler handler, BindingScope? scope)
    {
        if (scope is null)
            throw new ArgumentNullException(nameof(scope),
                "BindingScope is required to prevent event-lifetime leaks. " +
                "Pass the owning view's _bindingScope.");
        scope.Add(config, handler);
    }
}

internal sealed class PropertyAccessor
{
    private readonly object _root;
    private readonly List<MemberInfo> _chain;

    public string PropertyPath { get; }

    private readonly BindableSettings? _rootConfig;

    private PropertyAccessor(object root, List<MemberInfo> chain, BindableSettings? rootConfig, string propertyPath)
    {
        _root = root;
        _chain = chain;
        _rootConfig = rootConfig;
        PropertyPath = propertyPath;
    }

    private PropertyInfo? LeafProperty => _chain[_chain.Count - 1] as PropertyInfo;

    public object? GetValue() => LeafProperty?.GetValue(ResolveTarget());

    public void SetValue(object value)
    {
        var target = ResolveTarget();
        LeafProperty?.SetValue(target, value);

        if (_rootConfig is not null && !ReferenceEquals(target, _rootConfig))
            _rootConfig.RaisePropertyChanged(PropertyPath);
    }

    private object ResolveTarget()
    {
        object current = _root;
        for (int i = 0; i < _chain.Count - 1; i++)
        {
            if (_chain[i] is PropertyInfo p) current = p.GetValue(current);
            else if (_chain[i] is FieldInfo f) current = f.GetValue(current);
        }
        return current;
    }

    public static PropertyAccessor? FromExpression<TSource, TProperty>(TSource root, Expression<Func<TSource, TProperty>> expression)
    {
        var chain = new List<MemberInfo>();
        Expression current = expression.Body;

        while (current is MemberExpression memberExpr)
        {
            chain.Add(memberExpr.Member);
            current = memberExpr.Expression;
        }

        if (chain.Count is 0) return null;

        chain.Reverse();

        var pathParts = new System.Text.StringBuilder();
        for (int i = 0; i < chain.Count; i++)
        {
            if (i > 0) pathParts.Append('.');
            pathParts.Append(chain[i].Name);
        }

        var leafMember = chain[chain.Count - 1];
        if (leafMember is PropertyInfo)
            return new PropertyAccessor(root!, chain, (root as BindableSettings)!, pathParts.ToString());

        return null;
    }
}