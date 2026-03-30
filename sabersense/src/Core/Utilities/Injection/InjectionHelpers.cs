// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Data;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Core.Utilities.Injection;

internal static class InjectionHelpers
{
    internal static List<GameObject> FindSaberChildren(Transform root)
    {
        var children = new List<GameObject>();
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name.Equals("LeftSaber", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("RightSaber", System.StringComparison.OrdinalIgnoreCase))
            {
                children.Add(child.gameObject);
            }
        }
        return children;
    }

    internal static List<string?> ResolveObjectNames(
        IReadOnlyList<long> pathIds,
        SaberParseResult parseResult)
    {
        var names = new List<string?>(pathIds.Count);
        foreach (var pathId in pathIds)
        {
            if (pathId is not 0 && parseResult.PathIdToGameObjectName.TryGetValue(pathId, out var name))
                names.Add(name);
            else
                names.Add(null);
        }
        return names;
    }

    internal static Dictionary<string, List<Transform>> BuildTransformLookup(Transform root)
    {
        var lookup = new Dictionary<string, List<Transform>>();
        CollectTransforms(root, lookup);
        return lookup;
    }

    internal static void CollectTransforms(Transform current, Dictionary<string, List<Transform>> lookup)
    {
        if (!lookup.TryGetValue(current.name, out var list))
        {
            list = new List<Transform>(1);
            lookup[current.name] = list;
        }
        list.Add(current);

        for (int i = 0; i < current.childCount; i++)
            CollectTransforms(current.GetChild(i), lookup);
    }

    internal static void CollectTransformsFlat(Transform current, Dictionary<string, Transform> lookup)
    {
        lookup.TryAdd(current.name, current);
        for (int i = 0; i < current.childCount; i++)
            CollectTransformsFlat(current.GetChild(i), lookup);
    }

    internal static void CollectGameObjectsByName(Transform root, Dictionary<string, GameObject> lookup)
    {
        var transforms = new Dictionary<string, Transform>();
        CollectTransformsFlat(root, transforms);
        foreach (var (name, tf) in transforms)
            lookup.TryAdd(name, tf.gameObject);
    }

    internal static Dictionary<long, Transform> ResolveTransformPathIds(
        SaberParseResult parseResult,
        Dictionary<string, List<Transform>> transformsByName)
    {
        var map = new Dictionary<long, Transform>();
        if (parseResult?.PathIdToGameObjectName is null) return map;

        var redundantPathIds = new HashSet<long>();
        if (parseResult.TransformToGameObjectPathId is not null)
        {
            foreach (var (tfPathId, goPathId) in parseResult.TransformToGameObjectPathId)
            {
                if (parseResult.PathIdToGameObjectName.TryGetValue(tfPathId, out var tfName) &&
                    parseResult.PathIdToGameObjectName.TryGetValue(goPathId, out var goName) &&
                    tfName == goName)
                {
                    redundantPathIds.Add(tfPathId);
                }
            }
        }

        var consumedIndexByName = new Dictionary<string, int>();

        foreach (var (pathId, goName) in parseResult.PathIdToGameObjectName)
        {
            if (redundantPathIds.Contains(pathId)) continue;

            if (!transformsByName.TryGetValue(goName, out var transforms) || transforms.Count is 0)
                continue;

            consumedIndexByName.TryGetValue(goName, out var nextIndex);

            if (nextIndex < transforms.Count)
            {
                map[pathId] = transforms[nextIndex];
                consumedIndexByName[goName] = nextIndex + 1;
            }
            else
            {
                map[pathId] = transforms[0];
            }
        }

        if (parseResult.TransformToGameObjectPathId is not null)
        {
            foreach (var tfPathId in redundantPathIds)
            {
                if (parseResult.TransformToGameObjectPathId.TryGetValue(tfPathId, out var goPathId)
                    && map.TryGetValue(goPathId, out var resolved))
                {
                    map[tfPathId] = resolved;
                }
            }
        }

        return map;
    }

    internal static Transform? ResolveTransform(
        long pathId,
        Dictionary<long, Transform> pathIdToTransform)
    {
        if (pathId is 0) return null;

        if (pathIdToTransform.TryGetValue(pathId, out var transform))
            return transform;

        return null;
    }
}