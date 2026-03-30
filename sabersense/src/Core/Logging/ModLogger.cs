// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

namespace SaberSense.Core.Logging;

internal static class ModLogger
{
    private static IModLogger? _instance;

    internal static void Initialize(IModLogger logger) => _instance ??= logger;

    internal static IModLogger Instance => _instance ?? NullLogger.Instance;

    public static void Debug(string message) => _instance?.Debug(message);
    public static void Info(string message) => _instance?.Info(message);
    public static void Warn(string message) => _instance?.Warn(message);
    public static void Error(string message) => _instance?.Error(message);

    public static IModLogger ForSource(string source) => _instance?.ForSource(source) ?? NullLogger.Instance;
}