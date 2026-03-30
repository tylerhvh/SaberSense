// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Profiles;

public interface ISaberListEntry
{
    string DisplayName { get; }

    bool IsPinned { get; }

    string CreatorName { get; }

    Sprite? CoverImage { get; }

    bool IsSPICompatible { get; }
}

internal interface IAsyncLoadable
{
    Task? CurrentTask { get; }
}

internal interface IAssetParser
{
    SaberAssetEntry? ParseAsset(Catalog.LoadedBundle bundle);
}