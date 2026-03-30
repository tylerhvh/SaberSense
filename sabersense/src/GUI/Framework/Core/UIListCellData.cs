// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public class UIListCellData
{
    public string Title { get; set; }
    public string? Subtitle { get; set; }
    public Sprite? Icon { get; set; }
    public object? UserData { get; set; }
    public bool IsPinned { get; set; }

    public UIListCellData(string title, string? subtitle = "", Sprite? icon = null, object? userData = null, bool isPinned = false)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        UserData = userData;
        IsPinned = isPinned;
    }
}