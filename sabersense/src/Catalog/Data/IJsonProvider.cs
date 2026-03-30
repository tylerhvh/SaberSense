// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;

namespace SaberSense.Catalog.Data;

public interface IJsonProvider
{
    JsonSerializer Json { get; }
}