// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Rendering.TrailGeometry;
using UnityEngine;

namespace SaberSense.Rendering;

internal sealed class MotionBlurHistory
{
    private const int BufferSize = 8;

    private readonly Vector3[] _positions = new Vector3[BufferSize];
    private readonly Quaternion[] _rotations = new Quaternion[BufferSize];
    private readonly Vector3[] _scales = new Vector3[BufferSize];
    private int _head;
    private int _count;

    public int Count => _count;

    public int Capacity => BufferSize;

    public void Record(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        _positions[_head] = pos;
        _rotations[_head] = rot;
        _scales[_head] = scale;
        _head = (_head + 1) % BufferSize;
        if (_count < BufferSize) _count++;
    }

    public void Fill(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        for (int i = 0; i < BufferSize; i++)
        {
            _positions[i] = pos;
            _rotations[i] = rot;
            _scales[i] = scale;
        }
        _count = BufferSize;
        _head = 0;
    }

    public Vector3 Pos(int age)
    {
        age = Mathf.Clamp(age, 0, _count - 1);
        return _positions[((_head - 1 - age) % BufferSize + BufferSize) % BufferSize];
    }

    public Quaternion Rot(int age)
    {
        age = Mathf.Clamp(age, 0, _count - 1);
        return _rotations[((_head - 1 - age) % BufferSize + BufferSize) % BufferSize];
    }

    public Vector3 Scale(int age)
    {
        age = Mathf.Clamp(age, 0, _count - 1);
        return _scales[((_head - 1 - age) % BufferSize + BufferSize) % BufferSize];
    }

    public Matrix4x4 FrameMatrix(float frameT)
    {
        int seg = Mathf.FloorToInt(frameT);
        float lt = frameT - seg;
        seg = Mathf.Clamp(seg, 0, _count - 2);

        var p0 = Pos(Mathf.Max(seg - 1, 0));
        var p1 = Pos(seg);
        var p2 = Pos(seg + 1);
        var p3 = Pos(Mathf.Min(seg + 2, _count - 1));
        var pos = CatmullRomInterpolator.Interpolate(p0, p1, p2, p3, lt);

        var rot = Quaternion.Slerp(Rot(seg), Rot(seg + 1), lt);
        var scl = Vector3.Lerp(Scale(seg), Scale(seg + 1), lt);
        return Matrix4x4.TRS(pos, rot, scl);
    }
}