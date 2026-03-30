// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Menu.Builders;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.GUI.Framework.Menu.Tabs;

namespace SaberSense.GUI.Framework.Menu;

internal sealed record MenuBundle(

    SaberSelectionController Selection,
    SaberCatalogController Catalog,
    SaberTransformController Transform,
    MaterialEditingController Material,
    PreviewController Preview,
    SplitPopupManager SplitPopup,
    MaterialPropertyRowBuilder RowBuilder,
    TexturePropertyBuilder TextureBuilder,
    TrailSettingsController Trail,
    LogConsoleController Console,

    SaberTabView SaberTab,
    TrailTabView TrailTab,
    ModifierTabView ModifierTab,
    SettingsTabView SettingsTab,

    SaberSense.GUI.TrailVisualizationRenderer TrailPreviewer
);