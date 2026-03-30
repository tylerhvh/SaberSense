// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

namespace SaberSense.Core;

internal sealed class PauseKeyInputBehavior : XRButtonPoller<PauseKeyInputBehavior>
{
    private const string ObjectName = "SaberSense_PauseKeyTracker";

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
        if (ButtonBinding is 0)
        {
            CommitState(false);
            return;
        }

        CommitState(PollVRButton());
    }
}