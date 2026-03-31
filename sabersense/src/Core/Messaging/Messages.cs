// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using SaberSense.Profiles;
using SaberSense.Rendering;
using SaberSense.Services;

namespace SaberSense.Core.Messaging;

internal readonly record struct SaberLoadedMsg(SaberAssetEntry Entry);
internal readonly record struct SaberEquippedMsg(SaberAssetEntry Entry, EquipSource Source);
internal readonly record struct PreviewSaberChangedMsg(SaberAssetEntry Entry);
internal readonly record struct SaberPreviewInstantiatedMsg(LiveSaber Saber, SaberHand Hand);
internal readonly record struct SaberWidthChangedMsg(float Width);
internal readonly record struct TrailSettingsChangedMsg();

internal readonly record struct TrailMaterialEditedMsg(UnityEngine.Material Material);
internal readonly record struct SettingsChangedMsg();
internal readonly record struct ConfigLoadingMsg();
internal readonly record struct ConfigLoadedMsg();
internal readonly record struct CoverGeneratedMsg(AssetPreview Preview);
internal readonly record struct LogEntryMsg(LogEntry Entry);
internal readonly record struct PreviewsWipedMsg();
internal readonly record struct PreviewAddedMsg();
internal readonly record struct ScanCompleteMsg();