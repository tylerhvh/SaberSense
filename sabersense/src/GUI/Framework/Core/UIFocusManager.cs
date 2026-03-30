// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UIFocusManager
{
    private static UIFocusManager? _instance;

    public static UIFocusManager Instance => _instance ??= new();

    private readonly Stack<UIElement> _modalStack = new();
    private UIElement? _focusedControl;

    private UIFocusManager() { }

    public void PushModal(UIElement modal)
    {
        if (modal is null) return;
        _modalStack.Push(modal);
    }

    public void PopModal(UIElement modal)
    {
        if (_modalStack.Count is > 0 && _modalStack.Peek() == modal)
            _modalStack.Pop();
    }

    public bool HasActiveModal => _modalStack.Count is > 0;

    public UIElement? TopModal => _modalStack.Count is > 0 ? _modalStack.Peek() : null;

    public bool IsInputBlocked(UIElement target)
    {
        if (_modalStack.Count is 0 || target is null) return false;

        var topModal = _modalStack.Peek();
        if (target == topModal) return false;

        if (topModal.GameObject != null && target.GameObject != null)
        {
            return !target.GameObject.transform.IsChildOf(topModal.GameObject.transform);
        }
        return true;
    }

    public UIElement? FocusedControl => _focusedControl;

    public void SetFocus(UIElement control)
    {
        if (_focusedControl == control) return;
        _focusedControl = control;
    }

    public void ClearFocus(UIElement control)
    {
        if (_focusedControl == control)
            _focusedControl = null;
    }

    public void Reset()
    {
        _modalStack.Clear();
        _focusedControl = null;
    }
}