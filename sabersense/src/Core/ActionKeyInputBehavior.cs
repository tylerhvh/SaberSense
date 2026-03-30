// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Core;

internal sealed class ActionKeyInputBehavior : XRButtonPoller<ActionKeyInputBehavior>, IActionKeyProvider
{
    private const string ObjectName = "SaberSense_ActionKeyTracker";

    bool IActionKeyProvider.IsPressed => IsButtonPressed;
    bool IActionKeyProvider.IsPressedDown => IsButtonPressedDown;

    int IActionKeyProvider.Binding
    {
        get => ButtonBinding;
        set => ButtonBinding = value;
    }

    void IActionKeyProvider.Initialize() => EnsureInstance(ObjectName);
    void IActionKeyProvider.ResetState() => ResetInputState();

    public static bool IsPressed => Instance != null && Instance.IsButtonPressed;

    public static bool IsPressedDown => Instance != null && Instance.IsButtonPressedDown;

    public static int Binding
    {
        get => Instance?.ButtonBinding ?? 0;
        set { if (Instance != null) Instance.ButtonBinding = value; }
    }

    public static void Initialize() => EnsureInstance(ObjectName);

    public static void ResetState() => ResetInputState();

    private void Update()
    {
        bool rightClick = Input.GetMouseButton(1);
        bool rightClickDown = Input.GetMouseButtonDown(1);

        bool vrVal = PollVRButton();

        CommitState(rightClick || vrVal, rightClickDown);
    }
}