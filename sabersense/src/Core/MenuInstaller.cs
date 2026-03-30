// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Core.Patches;
using SaberSense.Customization;
using SaberSense.Gameplay;
using SaberSense.GUI;
using SaberSense.GUI.Framework.Menu;
using Zenject;

namespace SaberSense.Installers;

internal sealed class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<Customization.SaberEditor>().AsSingle()
            .OnInstantiated<Customization.SaberEditor>((_, editor) => HarmonyBridge.Editor = editor);
        Container.BindInterfacesAndSelfTo<SaberSenseMenuButton>().AsSingle()
            .OnInstantiated<SaberSenseMenuButton>((_, btn) => HarmonyBridge.MenuButton = btn);
        Container.Bind<PreviewSession>().AsSingle();
        Container.Bind<EditScope>().AsSingle();
        Container.Bind<GripAttachment>().AsSingle();

        Container.BindInterfacesAndSelfTo<TrailVisualizationRenderer>().AsSingle();
        Container.Bind<MenuSaberSpawner>().AsSingle();
        Container.Bind<ViewVisibilityService>().AsSingle();
        Container.BindInterfacesAndSelfTo<WorldModulationController>().AsSingle();

        Container.BindInterfacesAndSelfTo<MenuCameraRegistrator>().AsSingle();

        Container.Bind<Services.MaterialSyncService>().AsSingle();
        Container.Bind<Services.MaterialOverrideService>().AsSingle();
        Container.Bind<Services.OriginalMaterialCache>().AsSingle();
        Container.Bind<Services.SaberCatalogService>().AsSingle();
        Container.Bind<Services.CoverGenerationService>().AsSingle();

        Container.Bind<MenuControllerFactory>().AsSingle();
        Container.Bind<TrailMaterialSynchronizer>().AsSingle();
        Container.Bind<MenuEventWiring>().AsSingle();
    }
}