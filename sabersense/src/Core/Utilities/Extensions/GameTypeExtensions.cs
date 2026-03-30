// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Rendering;
using UnityEngine;

namespace SaberSense.Core.Utilities;

public static class GameTypeExtensions
{
    public static GameObject CreateGameObject(this Transform parent, string name, bool keepWorldPos = false)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, keepWorldPos);
        return child;
    }

    public static GameObject CreateGameObject(this GameObject parent, string name, bool keepWorldPos = false) =>
        parent.transform.CreateGameObject(name, keepWorldPos);

    public static void TryDestroy(this Object target)
    {
        if (target) Object.Destroy(target);
    }

    public static void TryDestroyImmediate(this Object target)
    {
        if (target) Object.DestroyImmediate(target);
    }

    public static float MeasureSpan(Transform first, Transform second) =>
        first && second ? Mathf.Abs(first.localPosition.z - second.localPosition.z) : 0f;

    public static float GetWidth(this SaberTrailMarker trail) =>
        trail ? MeasureSpan(trail.PointEnd!, trail.PointStart!) : 0f;

    public static void SetMaterial(this Renderer renderer, int index, Material newMat)
    {
        var materials = renderer.sharedMaterials;
        materials[index] = newMat;
        renderer.sharedMaterials = materials;
    }

    public static Vector2 With(in this Vector2 v, float? x = null, float? y = null) =>
        new(x ?? v.x, y ?? v.y);

    public static Vector3 With(in this Vector3 v, float? x = null, float? y = null, float? z = null) =>
        new(x ?? v.x, y ?? v.y, z ?? v.z);

    public static float[] ToArray(in this Color c) => [c.r, c.g, c.b, c.a];
    public static float[] ToArray(in this Vector2 v) => [v.x, v.y];
    public static float[] ToArray(in this Vector3 v) => [v.x, v.y, v.z];
    public static float[] ToArray(in this Vector4 v) => [v.x, v.y, v.z, v.w];

    public static float GetLastNoteTime(this BeatmapData map)
    {
        var last = 0f;
        foreach (var note in map.GetBeatmapDataItems<NoteData>(0))
        {
            if (note.colorType is ColorType.None) continue;
            if (note.time > last) last = note.time;
        }
        return last;
    }
}