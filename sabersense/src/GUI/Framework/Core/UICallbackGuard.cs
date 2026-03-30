// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Runtime.CompilerServices;

namespace SaberSense.GUI.Framework.Core;

internal static class UICallbackGuard
{
    public static void Invoke(Action action, [CallerMemberName] string caller = "")
    {
        if (action is null) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("UICallbackGuard").Error($"Callback failed in {caller}: {ex}");
        }
    }

    public static void Invoke<T>(Action<T> action, T arg, [CallerMemberName] string caller = "")
    {
        if (action is null) return;
        try
        {
            action(arg);
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("UICallbackGuard").Error($"Callback failed in {caller}: {ex}");
        }
    }

    public static void Invoke<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2, [CallerMemberName] string caller = "")
    {
        if (action is null) return;
        try
        {
            action(arg1, arg2);
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("UICallbackGuard").Error($"Callback failed in {caller}: {ex}");
        }
    }
}