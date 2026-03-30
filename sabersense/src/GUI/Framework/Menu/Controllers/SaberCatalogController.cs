// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Profiles;

namespace SaberSense.GUI.Framework.Menu.Controllers;

internal sealed class SaberCatalogController
{
    private readonly Services.SaberCatalogService _catalogService;
    private readonly SaberSelectionController _selectionController;

    private FolderNavigator? _dirManager;

    public enum ESortMode { Name, Date, Size, Author }

    private ESortMode _sortMode = ESortMode.Name;

    public SaberCatalogController(
        Services.SaberCatalogService catalogService,
        SaberSelectionController selectionController)
    {
        _catalogService = catalogService;
        _selectionController = selectionController;
    }

    public ESortMode SortMode
    {
        get => _sortMode;
        set => _sortMode = value;
    }

    public FolderNavigator? Folders => _dirManager;

    public void Init(FolderNavigator dirManager)
    {
        _dirManager = dirManager;
    }

    public void SetPinned(SaberAssetEntry entry, bool isOn)
    {
        _catalogService.SetPinned(entry, isOn);
    }
}