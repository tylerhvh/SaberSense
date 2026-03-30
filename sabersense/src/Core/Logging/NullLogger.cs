// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.Logging;

internal sealed class NullLogger : IModLogger
{
    internal static readonly NullLogger Instance = new();

    private NullLogger() { }

    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
    public void Error(Exception exception) { }

    public IModLogger ForSource(string source) => this;
}