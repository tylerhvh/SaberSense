// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.BundleFormat;
using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Catalog.Data;

public sealed partial class SaberBundleParser
{
    private readonly IModLogger _log;

    public SaberBundleParser(IModLogger log)
    {
        _log = log.ForSource(nameof(SaberBundleParser));
    }

    public SaberParseResult? Parse(string filePath)
    {
        try
        {
            var bundleContent = BundleReader.ExtractBundleContent(filePath);
            if (bundleContent.Count is 0) return null;

            byte[]? assetsData = null;
            foreach (var pair in bundleContent)
            {
                if (!pair.Key.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) &&
                    !pair.Key.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
                {
                    assetsData = pair.Value;
                    break;
                }
            }
            if (assetsData is null) return null;

            var assetsReader = new AssetsFileReader();
            assetsReader.Load(assetsData);

            var tables = BuildLookupTables(assetsReader);

            SaberMetadata? metadata = null;
            var trails = new List<TrailData>();
            long coverSpritePathId = 0;
            var modifierPayloads = new List<ModifierPayload>();
            var springBones = new List<SpringBoneEntry>();
            var springColliders = new List<SpringColliderEntry>();
            var eventManagers = new List<EventManagerEntry>();
            var comboFilters = new List<ComboFilterEntry>();
            var nthComboFilters = new List<EveryNthComboEntry>();
            var accuracyFilters = new List<AccuracyFilterEntry>();

            foreach (var info in assetsReader.Objects)
            {
                var type = assetsReader.GetType(info);
                if (type is null) continue;

                int typeId = type.TypeId;
                if (typeId is not 114 and >= 0) continue;

                var obj = assetsReader.ReadObject(info);
                if (obj is null) continue;

                var scriptRef = obj.GetChild("m_Script");
                if (scriptRef is null) continue;

                var scriptPathId = scriptRef.GetLong("m_PathID");
                if (!tables.ScriptMap.TryGetValue(scriptPathId, out var className)) continue;

                switch (className)
                {
                    case "SaberDescriptor":
                        var (desc, coverPId) = ReadSaberDescriptor(obj);
                        metadata = desc;
                        coverSpritePathId = coverPId;
                        break;

                    case "CustomTrail":
                        var trailData = ReadCustomTrail(obj);
                        trails.Add(trailData);
                        break;

                    case "SaberModifierCollection":
                        var modPayload = ReadModifierCollection(obj);
                        if (modPayload is not null)
                            modifierPayloads.Add(modPayload);
                        break;

                    case "DynamicBone":
                        springBones.Add(ReadDynamicBone(obj));
                        break;

                    case "DynamicBoneCollider":
                        springColliders.Add(ReadDynamicBoneCollider(obj, info.PathId));
                        break;

                    case "EventManager":
                        var evtMgr = ReadEventManager(obj);
                        if (evtMgr.HasAnyCalls)
                            eventManagers.Add(evtMgr);
                        break;

                    case "ComboReachedEvent":
                        comboFilters.Add(ReadComboFilter(obj));
                        break;

                    case "EveryNthComboFilter":
                        nthComboFilters.Add(ReadEveryNthComboFilter(obj));
                        break;

                    case "AccuracyReachedEvent":
                        accuracyFilters.Add(ReadAccuracyFilter(obj));
                        break;
                }
            }

            foreach (var trail in trails)
            {
                if (trail.PointEndPathId is not 0 && tables.Transforms.TryGetValue(trail.PointEndPathId, out var endXf))
                    trail.ParsedPointEndZ = endXf.Position.z;
                if (trail.PointStartPathId is not 0 && tables.Transforms.TryGetValue(trail.PointStartPathId, out var startXf))
                    trail.ParsedPointStartZ = startXf.Position.z;
            }

            var parsedBounds = ComputeParsedBounds(
                tables.Transforms, tables.MeshAABBs, tables.GoToMesh, tables.GoToTransform);
            if (parsedBounds.HasValue)
                _log?.Debug($"Parsed bounds: minZ={parsedBounds.Value.minZ:F3} maxZ={parsedBounds.Value.maxZ:F3}");

            CoverImageData? coverData = null;
            if (coverSpritePathId is not 0)
            {
                coverData = TryExtractCoverImage(assetsReader, coverSpritePathId, bundleContent);
            }

            return new(
                metadata ?? SaberMetadata.Unknown,
                trails,
                tables.GameObjectNames,
                tables.MaterialNames,
                coverData,
                modifierPayloads,
                parsedBounds,
                springBones,
                springColliders,
                tables.TransformToGameObject,
                (eventManagers.Count is > 0 || comboFilters.Count is > 0 || nthComboFilters.Count is > 0 || accuracyFilters.Count is > 0)
                    ? new()
                    {
                        EventManagers = eventManagers,
                        ComboFilters = comboFilters,
                        NthComboFilters = nthComboFilters,
                        AccuracyFilters = accuracyFilters,
                        PathIdToTypeId = tables.ObjectTypeIds
                    }
                    : null);
        }
        catch (Exception ex)
        {
            _log?.Warn($"Failed to parse '{filePath}': {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private struct ParsedTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public long ParentPathId;
        public long GameObjectPathId;
    }

    private struct ParsedAABB
    {
        public Vector3 Center;
        public Vector3 Extent;
    }

    private struct BundleLookupTables
    {
        public Dictionary<long, string> ScriptMap;

        public Dictionary<long, string> GameObjectNames;

        public Dictionary<long, string> MaterialNames;

        public Dictionary<long, ParsedTransform> Transforms;

        public Dictionary<long, ParsedAABB> MeshAABBs;

        public Dictionary<long, long> GoToMesh;

        public Dictionary<long, long> GoToTransform;

        public Dictionary<long, long> TransformToGameObject;

        public Dictionary<long, int> ObjectTypeIds;
    }
}