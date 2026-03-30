// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.XR;

namespace SaberSense.Core;

internal abstract class XRButtonPoller<T> : MonoBehaviour where T : XRButtonPoller<T>
{
    protected static T? Instance;

    private bool _isPressed;
    private bool _isPressedDown;
    private int _binding;

    protected bool IsButtonPressed => _isPressed;

    protected bool IsButtonPressedDown => _isPressedDown;

    protected int ButtonBinding
    {
        get => _binding;
        set => _binding = value;
    }

    protected static void EnsureInstance(string objectName)
    {
        if (Instance != null) return;
        var go = new GameObject(objectName);
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<T>();
    }

    protected static void ResetInputState()
    {
        if (Instance == null) return;
        Instance._isPressed = false;
        Instance._isPressedDown = false;
    }

    protected virtual void OnDestroy()
    {
        if (Instance == (T)this) Instance = null;
    }

    protected bool PollVRButton()
    {
        if (_binding is 0) return false;

        bool left = _binding % 2 is 1;
        var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);

        if (!device.isValid) return false;

        bool value = false;
        switch (_binding)
        {
            case 1: case 2: device.TryGetFeatureValue(CommonUsages.triggerButton, out value); break;
            case 3: case 4: device.TryGetFeatureValue(CommonUsages.gripButton, out value); break;
            case 5: case 6: device.TryGetFeatureValue(CommonUsages.primaryButton, out value); break;
            case 7: case 8: device.TryGetFeatureValue(CommonUsages.secondaryButton, out value); break;
            case 9: case 10: device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out value); break;
        }
        return value;
    }

    protected void CommitState(bool combined, bool extraDown = false)
    {
        _isPressedDown = (combined && !_isPressed) || extraDown;
        _isPressed = combined;
    }
}