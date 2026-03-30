// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Behaviors;
using SaberSense.Catalog.Data;
using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Core.Utilities.Injection;

internal static class ModifierInjector
{
    internal static void InjectModifiers(
        GameObject root,
        IReadOnlyList<ModifierPayload> payloads,
        SaberParseResult parseResult)
    {
        if (root == null || payloads is null || payloads.Count is 0) return;

        var saberChildren = InjectionHelpers.FindSaberChildren(root.transform);
        if (saberChildren.Count is 0) return;

        var transformsByName = InjectionHelpers.BuildTransformLookup(root.transform);
        var pathIdToTransform = InjectionHelpers.ResolveTransformPathIds(parseResult, transformsByName);

        var payloadsByChild = new Dictionary<GameObject, (List<string> allJson, List<string> allNames)>();

        foreach (var payload in payloads)
        {
            var targetChild = ResolvePayloadHost(
                payload, saberChildren, pathIdToTransform, parseResult);

            if (targetChild == null)
            {
                ModLogger.ForSource("PrefabInjector").Debug(
                    "Could not resolve modifier host, will inject onto first saber child");
                targetChild = saberChildren[0];
            }

            if (!payloadsByChild.TryGetValue(targetChild, out var bucket))
            {
                bucket = ([], []);
                payloadsByChild[targetChild] = bucket;
            }

            var targetNames = InjectionHelpers.ResolveObjectNames(payload.ObjectPathIds, parseResult);

            var indexRemap = new Dictionary<int, int>();
            for (int i = 0; i < targetNames.Count; i++)
            {
                var name = targetNames[i];
                int mergedIdx = bucket.allNames.IndexOf(name!);
                if (mergedIdx < 0)
                {
                    mergedIdx = bucket.allNames.Count;
                    bucket.allNames.Add(name!);
                }
                indexRemap[i] = mergedIdx;
            }

            bucket.allJson.Add(RemapObjectIndices(payload.DefinitionJson, indexRemap));
        }

        foreach (var (child, (jsonParts, names)) in payloadsByChild)
        {
            var host = child.GetComponent<SaberModifierHost>() ?? child.AddComponent<SaberModifierHost>();
            host.ModifierJson = MergeModifierJson(jsonParts);
            host.TargetObjectNames = names;
            host.Initialize();

            ModLogger.ForSource("PrefabInjector").Info(
                $"Injected SaberModifierHost onto '{child.name}' from {jsonParts.Count} payload(s), " +
                $"{names.Count} targets, " +
                $"{host.VisibilityRules?.Length ?? 0} visibility rules, " +
                $"{host.TransformRules?.Length ?? 0} transform rules");
        }

        MirrorModifiers(root.transform, saberChildren);
    }

    private static GameObject? ResolvePayloadHost(
        ModifierPayload payload,
        List<GameObject> saberChildren,
        Dictionary<long, Transform> pathIdToTransform,
        SaberParseResult parseResult)
    {
        if (payload.HostGameObjectPathId is 0) return null;

        if (pathIdToTransform.TryGetValue(payload.HostGameObjectPathId, out var resolved))
        {
            var current = resolved;
            while (current != null)
            {
                foreach (var child in saberChildren)
                {
                    if (current.gameObject == child) return child;
                }
                current = current.parent;
            }
        }

        if (parseResult.PathIdToGameObjectName.TryGetValue(payload.HostGameObjectPathId, out var name))
        {
            foreach (var child in saberChildren)
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }
        }

        return null;
    }

    private static string MergeModifierJson(List<string> jsonParts)
    {
        if (jsonParts.Count is 1) return jsonParts[0];

        var merged = new Newtonsoft.Json.Linq.JObject();
        foreach (var json in jsonParts)
        {
            if (string.IsNullOrEmpty(json)) continue;
            try
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value is Newtonsoft.Json.Linq.JArray srcArray)
                    {
                        if (merged[prop.Name] is not Newtonsoft.Json.Linq.JArray existing)
                        {
                            merged[prop.Name] = new Newtonsoft.Json.Linq.JArray(srcArray);
                        }
                        else
                        {
                            foreach (var item in srcArray)
                                existing.Add(item);
                        }
                    }
                    else if (!merged.ContainsKey(prop.Name))
                    {
                        merged[prop.Name] = prop.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.ForSource("PrefabInjector").Debug($"Failed to merge modifier JSON: {ex.Message}");
            }
        }
        return merged.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string RemapObjectIndices(string json, Dictionary<int, int> remap)
    {
        if (string.IsNullOrEmpty(json) || remap.Count is 0) return json;
        try
        {
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            bool changed = false;
            foreach (var prop in obj.Properties())
            {
                if (prop.Value is not Newtonsoft.Json.Linq.JArray rulesArray) continue;
                foreach (var rule in rulesArray)
                {
                    if (rule is not Newtonsoft.Json.Linq.JObject ruleObj) continue;

                    if (ruleObj["ObjectIndecies"] is Newtonsoft.Json.Linq.JArray indices)
                    {
                        var remapped = new Newtonsoft.Json.Linq.JArray();
                        foreach (var idx in indices)
                        {
                            int old = idx.ToObject<int>();
                            remapped.Add(remap.TryGetValue(old, out var mapped) ? mapped : old);
                        }
                        ruleObj["ObjectIndecies"] = remapped;
                        changed = true;
                    }

                    if (ruleObj["ObjectIndex"] is Newtonsoft.Json.Linq.JValue objIdx)
                    {
                        int old = objIdx.ToObject<int>();
                        if (remap.TryGetValue(old, out var mapped))
                        {
                            ruleObj["ObjectIndex"] = mapped;
                            changed = true;
                        }
                    }
                }
            }
            return changed ? obj.ToString(Newtonsoft.Json.Formatting.None) : json;
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("PrefabInjector").Debug($"Failed to remap modifier indices: {ex.Message}");
            return json;
        }
    }

    private static void MirrorModifiers(Transform _, List<GameObject> saberChildren)
    {
        if (saberChildren.Count is < 2) return;

        var left = saberChildren.Find(c => c.name.Equals("LeftSaber", System.StringComparison.OrdinalIgnoreCase));
        var right = saberChildren.Find(c => c.name.Equals("RightSaber", System.StringComparison.OrdinalIgnoreCase));
        if (left == null || right == null) return;

        var leftHost = left.GetComponent<SaberModifierHost>();
        var rightHost = right.GetComponent<SaberModifierHost>();

        if ((leftHost != null) == (rightHost != null)) return;

        var source = leftHost ?? rightHost;
        var target = leftHost != null ? right : left;

        var mirrored = target.AddComponent<SaberModifierHost>();
        mirrored.ModifierJson = source!.ModifierJson;
        mirrored.TargetObjectNames = [.. source.TargetObjectNames];
        mirrored.Initialize();

        ModLogger.ForSource("PrefabInjector").Info(
            $"Mirrored SaberModifierHost from '{source.gameObject.name}' to '{target.name}'");
    }
}