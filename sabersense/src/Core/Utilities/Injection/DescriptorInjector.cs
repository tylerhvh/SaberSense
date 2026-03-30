// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Data;
using UnityEngine;

namespace SaberSense.Core.Utilities.Injection;

internal static class DescriptorInjector
{
    internal static SaberDescriptor? InjectDescriptor(GameObject root, SaberMetadata metadata)
    {
        if (root == null || metadata is null) return null;

        var descriptor = root.GetComponent<SaberDescriptor>() ?? root.AddComponent<SaberDescriptor>();
        descriptor.SaberName = metadata.Name;
        descriptor.AuthorName = metadata.Author;
        descriptor.Description = metadata.Description;
        descriptor.CoverImage = metadata.CoverImage;
        return descriptor;
    }
}