// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.Logging;

public interface IModLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Error(Exception exception);

    IModLogger ForSource(string source);
}