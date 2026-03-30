// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.XR;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UIKeybindListener : MonoBehaviour
{
    public Action<int>? OnCaptured;

    public Action? OnCancelled;

    private bool _captured;
    private bool _armed;

    private void Update()
    {
        if (_captured) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Finish(0);
            return;
        }

        var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (!_armed)
        {
            if (!AnyButtonHeld(leftDevice, rightDevice))
                _armed = true;
            return;
        }

        if (TryCapture(leftDevice, CommonUsages.triggerButton, 1)) return;
        if (TryCapture(rightDevice, CommonUsages.triggerButton, 2)) return;

        if (TryCapture(leftDevice, CommonUsages.gripButton, 3)) return;
        if (TryCapture(rightDevice, CommonUsages.gripButton, 4)) return;

        if (TryCapture(leftDevice, CommonUsages.primaryButton, 5)) return;
        if (TryCapture(rightDevice, CommonUsages.primaryButton, 6)) return;

        if (TryCapture(leftDevice, CommonUsages.secondaryButton, 7)) return;
        if (TryCapture(rightDevice, CommonUsages.secondaryButton, 8)) return;

        if (TryCapture(leftDevice, CommonUsages.primary2DAxisClick, 9)) return;
        if (TryCapture(rightDevice, CommonUsages.primary2DAxisClick, 10)) return;
    }

    private static bool AnyButtonHeld(InputDevice left, InputDevice right)
    {
        return IsHeld(left, CommonUsages.triggerButton) || IsHeld(right, CommonUsages.triggerButton)
            || IsHeld(left, CommonUsages.gripButton) || IsHeld(right, CommonUsages.gripButton)
            || IsHeld(left, CommonUsages.primaryButton) || IsHeld(right, CommonUsages.primaryButton)
            || IsHeld(left, CommonUsages.secondaryButton) || IsHeld(right, CommonUsages.secondaryButton)
            || IsHeld(left, CommonUsages.primary2DAxisClick) || IsHeld(right, CommonUsages.primary2DAxisClick);
    }

    private static bool IsHeld(InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool v) && v;
    }

    private bool TryCapture(InputDevice device, InputFeatureUsage<bool> usage, int bindingIndex)
    {
        if (!device.isValid) return false;
        if (device.TryGetFeatureValue(usage, out bool pressed) && pressed)
        {
            Finish(bindingIndex);
            return true;
        }
        return false;
    }

    private void Finish(int index)
    {
        _captured = true;
        if (index > 0)
            OnCaptured?.Invoke(index);
        else
            OnCancelled?.Invoke();
        Destroy(this);
    }

    public void Cancel()
    {
        if (_captured) return;
        _captured = true;
        OnCancelled?.Invoke();
        Destroy(this);
    }
}