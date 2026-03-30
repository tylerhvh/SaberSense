// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Gameplay;

internal interface IDefaultSaberProvider
{
    GameObject? DefaultSaberPrefab { get; }
    GameObject? VanillaSaberPrefab { get; }
}