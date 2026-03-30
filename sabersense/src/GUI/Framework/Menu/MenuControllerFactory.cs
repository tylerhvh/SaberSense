// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Data;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Menu.Builders;
using SaberSense.GUI.Framework.Menu.Controllers;
using SaberSense.GUI.Framework.Menu.Tabs;
using SaberSense.Profiles;
using SaberSense.Rendering;

namespace SaberSense.GUI.Framework.Menu;

internal sealed class MenuControllerFactory
{
    private readonly PreviewSession _previewSession;
    private readonly ModSettings _pluginConfig;
    private readonly SaberEditor _editor;
    private readonly EditScope _editScope;
    private readonly IMessageBroker _broker;
    private readonly Services.MaterialOverrideService _overrideService;
    private readonly Services.MaterialSyncService _syncService;
    private readonly Services.OriginalMaterialCache _originalCache;
    private readonly SaberSense.Rendering.Shaders.ShaderIntrospector _shaderCache;
    private readonly PlayerDataModel _playerDataModel;
    private readonly Services.CoverGenerationService _coverService;
    private readonly LiveSaber.Factory _liveSaberCreator;
    private readonly Services.SaberCatalogService _catalogService;
    private readonly TextureCacheRegistry _textureRegistry;
    private readonly Serializer _serializer;
    private readonly IModLogger _log;
    private readonly SaberSense.GUI.TrailVisualizationRenderer _trailPreviewer;
    private readonly SaberLoadout _saberSet;
    private readonly InternalConfig _internalConfig;
    private readonly AppPaths _appPaths;
    private readonly Services.ConfigManager _configManager;
    private readonly DefaultSaberProvider _defaultSaberProvider;
    private readonly LogRingBuffer _ringBuffer;
    private readonly LogFileWriter _fileWriter;

    public MenuControllerFactory(
        PreviewSession previewSession,
        ModSettings pluginConfig,
        SaberEditor editor,
        EditScope editScope,
        IMessageBroker broker,
        Services.MaterialOverrideService overrideService,
        Services.MaterialSyncService syncService,
        Services.OriginalMaterialCache originalCache,
        SaberSense.Rendering.Shaders.ShaderIntrospector shaderCache,
        PlayerDataModel playerDataModel,
        Services.CoverGenerationService coverService,
        LiveSaber.Factory liveSaberCreator,
        Services.SaberCatalogService catalogService,
        TextureCacheRegistry textureRegistry,
        Serializer serializer,
        IModLogger log,
        SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
        SaberLoadout saberSet,
        InternalConfig internalConfig,
        AppPaths appPaths,
        Services.ConfigManager configManager,
        DefaultSaberProvider defaultSaberProvider,
        LogRingBuffer ringBuffer,
        LogFileWriter fileWriter)
    {
        _previewSession = previewSession;
        _pluginConfig = pluginConfig;
        _editor = editor;
        _editScope = editScope;
        _broker = broker;
        _overrideService = overrideService;
        _syncService = syncService;
        _originalCache = originalCache;
        _shaderCache = shaderCache;
        _playerDataModel = playerDataModel;
        _coverService = coverService;
        _liveSaberCreator = liveSaberCreator;
        _catalogService = catalogService;
        _textureRegistry = textureRegistry;
        _serializer = serializer;
        _log = log.ForSource(nameof(MenuControllerFactory));
        _trailPreviewer = trailPreviewer;
        _saberSet = saberSet;
        _internalConfig = internalConfig;
        _appPaths = appPaths;
        _configManager = configManager;
        _defaultSaberProvider = defaultSaberProvider;
        _ringBuffer = ringBuffer;
        _fileWriter = fileWriter;
    }

    public MenuBundle CreateAll(SaberCatalog catalog, FolderNavigator? dirManager, IModLogger viewLog)
    {
        var selection = new SaberSelectionController(_previewSession, _pluginConfig);
        selection.Init(catalog);

        var catalogCtrl = new SaberCatalogController(_catalogService, selection);
        catalogCtrl.Init(dirManager!);

        var transform = new SaberTransformController(_saberSet, _previewSession, _editScope, _broker);
        var material = new MaterialEditingController(_overrideService, _syncService, _originalCache, _shaderCache, _saberSet, _previewSession, _playerDataModel, _editScope);
        var preview = new PreviewController(_previewSession, _trailPreviewer, _editor, _pluginConfig, _playerDataModel, _coverService, _liveSaberCreator, _editScope, _broker, _previewSession.MaterialPool);
        var trail = new TrailSettingsController(_saberSet, _previewSession, _editScope, _broker);
        var console = new LogConsoleController(_ringBuffer, _fileWriter, _broker);

        var splitPopup = new SplitPopupManager(material, selection, _serializer);
        var toggleBuilder = new TogglePropertyBuilder(material, splitPopup, selection, _serializer, _previewSession);
        var floatBuilder = new FloatPropertyBuilder(material, splitPopup, selection, _serializer, _previewSession);
        var colorBuilder = new ColorPropertyBuilder(material, splitPopup, selection, _serializer, _previewSession);
        var textureBuilder = new TexturePropertyBuilder(material, splitPopup, selection, _serializer, _previewSession, _textureRegistry[TextureCategory.Saber], viewLog);
        var rowBuilder = new MaterialPropertyRowBuilder(material, splitPopup, colorBuilder, floatBuilder, toggleBuilder, textureBuilder, _previewSession);

        var saberTab = new SaberTabView(selection, catalogCtrl, transform,
            preview, _pluginConfig, _previewSession, _trailPreviewer, _editor, catalog, _broker, viewLog);
        var trailTab = new TrailTabView(selection,
            _pluginConfig, _previewSession, trail, _trailPreviewer, catalog, _catalogService, _broker);
        var modifierTab = new ModifierTabView(selection, material, rowBuilder,
            _previewSession, _saberSet, _broker, _serializer);
        var settingsTab = new SettingsTabView(_pluginConfig, _internalConfig, _appPaths, _broker, _defaultSaberProvider, _configManager, trail, transform, _previewSession, _saberSet, viewLog);

        return new MenuBundle(
            Selection: selection,
            Catalog: catalogCtrl,
            Transform: transform,
            Material: material,
            Preview: preview,
            SplitPopup: splitPopup,
            RowBuilder: rowBuilder,
            TextureBuilder: textureBuilder,
            Trail: trail,
            Console: console,
            SaberTab: saberTab,
            TrailTab: trailTab,
            ModifierTab: modifierTab,
            SettingsTab: settingsTab,
            TrailPreviewer: _trailPreviewer);
    }
}