// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Profiles;

[System.Serializable]
public readonly struct AssetTypeTag : System.IEquatable<AssetTypeTag>
{
    public static readonly AssetTypeTag SaberAsset = new(AssetKind.Model, PartCategory.SaberAsset);

    public AssetKind Kind { get; }
    public PartCategory Category { get; }

    public AssetTypeTag(AssetKind kind, PartCategory category)
    {
        Kind = kind;
        Category = category;
    }

    public bool Equals(AssetTypeTag other) => Kind == other.Kind && Category == other.Category;
    public override bool Equals(object obj) => obj is AssetTypeTag other && Equals(other);
    public override int GetHashCode() => System.HashCode.Combine(Kind, Category);
    public static bool operator ==(AssetTypeTag left, AssetTypeTag right) => left.Equals(right);
    public static bool operator !=(AssetTypeTag left, AssetTypeTag right) => !left.Equals(right);
}

public readonly struct SaberDisplayInfo
{
    public string? Name { get; }
    public string? Author { get; }
    public Sprite? Cover { get; }
    public bool IsPinned { get; }

    public SaberDisplayInfo(string name, string author, Sprite? cover, bool isPinned)
    {
        Name = name;
        Author = author;
        Cover = cover;
        IsPinned = isPinned;
    }
}

[Newtonsoft.Json.JsonObject]
public sealed class SaberScale
{
    public static SaberScale Unit => new();
    public float Length { get; set; } = 1f;
    public float Width { get; set; } = 1f;
}

public sealed class FolderEntry : ISaberListEntry
{
    public string DisplayName { get; }
    public string CreatorName => string.Empty;
    public Sprite? CoverImage => null;
    public bool IsPinned => false;
    public bool IsSPICompatible => true;

    public FolderEntry(string folderName) => DisplayName = folderName;
}