// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.Core.BundleFormat;

internal sealed class SerializedObject
{
    public long PathId { get; set; }

    public int TypeId { get; set; }

    private readonly Dictionary<string, object> _fields = [];

    public object? this[string fieldName] =>
        _fields.TryGetValue(fieldName, out var value) ? value : null;

    public string GetString(string fieldName, string fallback = "") =>
        _fields.TryGetValue(fieldName, out var value) && value is string s ? s : fallback;

    public int GetInt(string fieldName, int fallback = 0) =>
        _fields.TryGetValue(fieldName, out var value) ? (int)ConvertToLong(value, fallback) : fallback;

    public long GetLong(string fieldName, long fallback = 0) =>
        _fields.TryGetValue(fieldName, out var value) ? ConvertToLong(value, fallback) : fallback;

    public float GetFloat(string fieldName, float fallback = 0f) =>
        _fields.TryGetValue(fieldName, out var value) && value is float f ? f : fallback;

    public SerializedObject? GetChild(string fieldName) =>
        _fields.TryGetValue(fieldName, out var value) && value is SerializedObject child ? child : null;

    public byte[]? GetBytes(string fieldName) =>
        _fields.TryGetValue(fieldName, out var value) && value is byte[] bytes ? bytes : null;

    public void SetField(string name, object value) => _fields[name] = value;

    public bool HasFields => _fields.Count is > 0;

    public IEnumerable<string> FieldNames => _fields.Keys;

    private static long ConvertToLong(object value, long fallback)
    {
        return value switch
        {
            long l => l,
            ulong ul => (long)ul,
            int i => i,
            uint u => u,
            short s => s,
            ushort us => us,
            byte b => b,
            sbyte sb => sb,
            float f => (long)f,
            double d => (long)d,
            _ => fallback
        };
    }
}