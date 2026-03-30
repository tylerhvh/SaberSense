// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SaberSense.Core.BundleFormat;

internal sealed class AssetsFileReader
{
    private SerializedType[] _types = [];
    private ObjectInfo[] _objects = [];
    private byte[] _data = [];
    private long _dataOffset;
    private int _formatVersion;
    private bool _bigEndian;

    private static readonly Dictionary<int, string> CommonStrings = BuildCommonStringTable();

    public IReadOnlyList<ObjectInfo> Objects => _objects;

    public void Load(byte[] assetsData)
    {
        using var stream = new MemoryStream(assetsData);
        using var reader = new EndianReader(stream, bigEndian: true);

        int metadataSize = reader.ReadInt32();
        long fileSize = reader.ReadInt32();
        _formatVersion = reader.ReadInt32();
        _dataOffset = reader.ReadInt32();

        if (_formatVersion >= 22)
        {
            metadataSize = reader.ReadInt32();
            fileSize = reader.ReadInt64();
            _dataOffset = reader.ReadInt64();
            reader.ReadInt64();
        }

        if (_formatVersion >= 9)
        {
            var endianness = reader.ReadByte();
            reader.ReadBytes(3);
            _bigEndian = endianness != 0;
            reader.BigEndian = _bigEndian;
        }
        else
        {
            reader.BigEndian = false;
        }

        var unityVersion = reader.ReadNullTerminated();
        var targetPlatform = reader.ReadInt32();
        var typeTreeEnabled = reader.ReadBoolean();

        if (!typeTreeEnabled)
            throw new NotSupportedException("Assets file has no embedded type tree - cannot parse");

        int typeCount = reader.ReadInt32();
        if (typeCount < 0 || typeCount > 1000)
            throw new InvalidDataException($"Unreasonable type count: {typeCount}");

        _types = new SerializedType[typeCount];
        for (int i = 0; i < typeCount; i++) _types[i] = ReadSerializedType(reader);

        int objectCount = reader.ReadInt32();
        if (objectCount < 0 || objectCount > 100000)
            throw new InvalidDataException($"Unreasonable object count: {objectCount}");

        ModLogger.ForSource("AssetsFile").Info($"Loaded {unityVersion} (fmt {_formatVersion}) | types={typeCount}, objects={objectCount}");

        _objects = new ObjectInfo[objectCount];

        for (int i = 0; i < objectCount; i++)
        {
            _objects[i] = ReadObjectInfo(reader, _formatVersion);
        }

        for (int i = 0; i < _objects.Length; i++)
        {
            var obj = _objects[i];
            if (obj.TypeId == -1 && obj.TypeIndex >= 0 && obj.TypeIndex < _types.Length)
            {
                obj.TypeId = _types[obj.TypeIndex].TypeId;
            }
        }

        SkipRemainingMetadata(reader);

        if (_dataOffset <= 0 || _dataOffset >= assetsData.Length)
        {
            _dataOffset = (reader.Position + 15) & ~15L;

            if (_dataOffset >= assetsData.Length)
                _dataOffset = reader.Position;
        }

        _data = assetsData;
    }

    private void SkipRemainingMetadata(EndianReader reader)
    {
        try
        {
            if (_formatVersion >= 11)
            {
                int scriptCount = reader.ReadInt32();
                for (int i = 0; i < scriptCount; i++)
                {
                    int localFileIndex = reader.ReadInt32();
                    if (_formatVersion >= 14)
                    {
                        reader.Align4();
                        reader.ReadInt64();
                    }
                    else
                    {
                        reader.ReadInt32();
                    }
                }
            }

            int externalsCount = reader.ReadInt32();
            for (int i = 0; i < externalsCount; i++)
            {
                if (_formatVersion >= 6) reader.ReadNullTerminated();
                reader.ReadBytes(16);
                reader.ReadInt32();
                reader.ReadNullTerminated();
            }

            if (_formatVersion >= 20)
            {
                int refTypesCount = reader.ReadInt32();
                for (int i = 0; i < refTypesCount; i++)
                {
                    int classId = reader.ReadInt32();
                    if (_formatVersion >= 16) reader.ReadByte();
                    if (_formatVersion >= 17) reader.ReadInt16();

                    if ((_formatVersion >= 16 && classId == 114) ||
                        (_formatVersion >= 17 && classId < 0))
                    {
                        reader.ReadBytes(16);
                    }
                    reader.ReadBytes(16);
                }
            }

            if (_formatVersion >= 5)
            {
                reader.ReadNullTerminated();
            }
        }
        catch
        {
        }
    }

    public SerializedObject? ReadObject(ObjectInfo info)
    {
        if (info.TypeIndex < 0 || info.TypeIndex >= _types.Length)
            return null;

        var type = _types[info.TypeIndex];
        if (type.RootNode is null)
            return null;

        using var stream = new MemoryStream(_data);
        using var reader = new EndianReader(stream, bigEndian: _bigEndian);
        reader.Position = _dataOffset + info.ByteOffset;

        var result = new SerializedObject
        {
            PathId = info.PathId,
            TypeId = info.TypeId
        };

        ReadFields(reader, type.RootNode, result);
        return result;
    }

    public SerializedType? GetType(ObjectInfo info) =>
        info.TypeIndex >= 0 && info.TypeIndex < _types.Length ? _types[info.TypeIndex] : null;

    #region Type Tree Parsing

    private SerializedType ReadSerializedType(EndianReader reader)
    {
        long startPos = reader.Position;
        var type = new SerializedType
        {
            TypeId = reader.ReadInt32()
        };

        if (_formatVersion >= 16)
        {
            reader.ReadByte();
        }

        if (_formatVersion >= 17)
        {
            reader.ReadInt16();
        }

        if (_formatVersion >= 13)
        {
            if ((_formatVersion < 16 && type.TypeId < 0) || (_formatVersion >= 16 && type.TypeId == 114) || (_formatVersion >= 17 && type.TypeId < 0))
            {
                reader.ReadBytes(16);
            }
            reader.ReadBytes(16);
        }

        int nodeCount = reader.ReadInt32();
        int stringTableSize = reader.ReadInt32();

        if (nodeCount < 0 || nodeCount > 10000)
            throw new InvalidDataException(
                $"Unreasonable nodeCount={nodeCount} for typeId={type.TypeId} at pos={startPos}");
        if (stringTableSize < 0 || stringTableSize > 1_000_000)
            throw new InvalidDataException(
                $"Unreasonable stringTableSize={stringTableSize} for typeId={type.TypeId} at pos={startPos}");

        var nodes = new TypeTreeNodeRaw[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = new TypeTreeNodeRaw
            {
                Version = reader.ReadUInt16(),
                Depth = reader.ReadByte(),
                TypeFlags = reader.ReadByte(),
                TypeStringOffset = reader.ReadInt32(),
                NameStringOffset = reader.ReadInt32(),
                ByteSize = reader.ReadInt32(),
                Index = reader.ReadInt32(),
                MetaFlags = reader.ReadInt32()
            };

            if (_formatVersion >= 18)
            {
                reader.ReadBytes(8);
            }
        }

        var stringTableBytes = reader.ReadBytes(stringTableSize);
        var localStrings = ParseStringTable(stringTableBytes);

        if (_formatVersion >= 21)
        {
            int depCount = reader.ReadInt32();
            if (depCount > 0)
                reader.ReadBytes(depCount * 4);
        }

        type.RootNode = BuildTypeTree(nodes, localStrings);

        return type;
    }

    private TypeTreeNode? BuildTypeTree(TypeTreeNodeRaw[] rawNodes, Dictionary<int, string> localStrings)
    {
        if (rawNodes.Length is 0) return null;

        var allNodes = new TypeTreeNode[rawNodes.Length];
        for (int i = 0; i < rawNodes.Length; i++)
        {
            var raw = rawNodes[i];
            allNodes[i] = new TypeTreeNode
            {
                TypeName = ResolveString(raw.TypeStringOffset, localStrings),
                FieldName = ResolveString(raw.NameStringOffset, localStrings),
                ByteSize = raw.ByteSize,
                Depth = raw.Depth,
                IsAligned = (raw.MetaFlags & 0x4000) != 0,
                IsArray = (raw.TypeFlags & 0x01) != 0,
                Index = raw.Index
            };
        }

        var stack = new Stack<TypeTreeNode>();
        var root = allNodes[0];
        stack.Push(root);

        for (int i = 1; i < allNodes.Length; i++)
        {
            var node = allNodes[i];
            while (stack.Count is > 0 && stack.Peek().Depth >= node.Depth)
                stack.Pop();

            if (stack.Count is > 0)
                stack.Peek().Children.Add(node);

            stack.Push(node);
        }

        return root;
    }

    private string ResolveString(int offset, Dictionary<int, string> localStrings)
    {
        if ((offset & 0x80000000) != 0)
        {
            var commonOffset = offset & 0x7FFFFFFF;
            return CommonStrings.TryGetValue(commonOffset, out var s) ? s : $"unknown_{commonOffset}";
        }
        return localStrings.TryGetValue(offset, out var local) ? local : $"local_{offset}";
    }

    private static Dictionary<int, string> ParseStringTable(byte[] data)
    {
        var result = new Dictionary<int, string>();
        int i = 0;
        while (i < data.Length)
        {
            int start = i;
            while (i < data.Length && data[i] != 0) i++;
            if (i > start)
                result[start] = Encoding.UTF8.GetString(data, start, i - start);
            i++;
        }
        return result;
    }

    #endregion

    #region Object Data Reading

    private void ReadFields(EndianReader reader, TypeTreeNode rootNode, SerializedObject target)
    {
        foreach (var child in rootNode.Children)
        {
            try
            {
                var value = ReadFieldValue(reader, child);
                if (value is not null) target.SetField(child.FieldName, value);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Stream desync at {child.FieldName} ({child.TypeName}): {ex.Message}", ex);
            }
        }
    }

    private object? ReadFieldValue(EndianReader reader, TypeTreeNode node)
    {
        object? value;

        if (node.Children.Count is 0)
        {
            value = ReadPrimitive(reader, node);
        }

        else if (node.TypeName == "string")
        {
            return reader.ReadAlignedString();
        }

        else if (node.Children.Count is 1 && node.Children[0].FieldName == "Array")
        {
            value = ReadFieldValue(reader, node.Children[0]);
        }

        else if (node.IsArray)
        {
            value = ReadArray(reader, node);
        }

        else
        {
            var obj = new SerializedObject { TypeId = 0, PathId = 0 };
            ReadFields(reader, node, obj);
            value = obj;
        }

        if (node.IsAligned) reader.Align4();

        return value;
    }

    private object? ReadPrimitive(EndianReader reader, TypeTreeNode node)
    {
        return node.TypeName switch
        {
            "bool" => reader.ReadBoolean(),
            "SInt8" or "char" => (int)reader.ReadByte(),
            "UInt8" => (int)reader.ReadByte(),
            "SInt16" or "short" => (int)reader.ReadInt16(),
            "UInt16" or "unsigned short" => (int)reader.ReadUInt16(),
            "SInt32" or "int" => reader.ReadInt32(),
            "UInt32" or "unsigned int" => (long)reader.ReadUInt32(),
            "SInt64" or "long long" => reader.ReadInt64(),
            "UInt64" or "unsigned long long" => reader.ReadInt64(),
            "float" => reader.ReadFloat(),
            "double" => BitConverter.Int64BitsToDouble(reader.ReadInt64()),
            _ => ReadBySize(reader, node.ByteSize)
        };
    }

    private object? ReadBySize(EndianReader reader, int byteSize)
    {
        if (byteSize <= 0) return null;
        return reader.ReadBytes(byteSize);
    }

    private object? ReadArray(EndianReader reader, TypeTreeNode node)
    {
        TypeTreeNode? elementNode = null;
        if (node.Children.Count is >= 2)
        {
            elementNode = node.Children[1];
        }
        else if (node.Children.Count is 1 && node.Children[0].TypeName != "Array")
        {
            elementNode = node.Children[0];
        }

        if (elementNode is null) return null;

        int count = reader.ReadInt32();

        if (count is < 0)
            throw new InvalidDataException($"Negative array count {count} at position {reader.Position - 4}");

        if (elementNode.TypeName is "UInt8" or "SInt8" or "char" or "byte")
        {
            long remaining = reader.Length - reader.Position;
            if (count > remaining)
                throw new InvalidDataException(
                    $"Byte array count {count} exceeds remaining stream ({remaining} bytes)");
            return reader.ReadBytes(count);
        }

        if (count > 10_000_000)
            throw new InvalidDataException($"Unreasonable array count {count} at position {reader.Position - 4}");

        var list = new List<object>(Math.Min(count, 1024));
        for (int i = 0; i < count; i++)
        {
            list.Add(ReadFieldValue(reader, elementNode)!);
        }
        return list;
    }

    #endregion

    #region Object Info Parsing

    private static ObjectInfo ReadObjectInfo(EndianReader reader, int formatVersion)
    {
        var info = new ObjectInfo();

        if (formatVersion >= 14)
        {
            reader.Align4();
            info.PathId = reader.ReadInt64();
        }
        else
        {
            info.PathId = reader.ReadInt32();
        }

        if (formatVersion >= 22)
        {
            info.ByteOffset = reader.ReadInt64();
        }
        else
        {
            info.ByteOffset = reader.ReadInt32();
        }

        info.ByteSize = reader.ReadInt32();
        info.TypeIndex = reader.ReadInt32();

        if (formatVersion < 16)
        {
            info.TypeId = reader.ReadUInt16();
            reader.ReadInt16();
        }
        else
        {
            info.TypeId = -1;
        }

        if (formatVersion >= 11 && formatVersion < 17)
        {
            reader.ReadBoolean();
        }

        return info;
    }

    #endregion

    #region Common String Table

    private static Dictionary<int, string> BuildCommonStringTable()
    {
        return new Dictionary<int, string>
        {
            [0] = "AABB",
            [5] = "AnimationClip",
            [19] = "AnimationCurve",
            [34] = "AnimationState",
            [49] = "Array",
            [55] = "Base",
            [60] = "BitField",
            [69] = "bitset",
            [76] = "bool",
            [81] = "char",
            [86] = "ColorRGBA",
            [96] = "Component",
            [106] = "data",
            [111] = "deque",
            [117] = "double",
            [124] = "dynamic_array",
            [138] = "FastPropertyName",
            [155] = "first",
            [161] = "float",
            [167] = "Font",
            [172] = "GameObject",
            [183] = "Generic Mono",
            [196] = "GradientNEW",
            [208] = "GUID",
            [213] = "GUIStyle",
            [222] = "int",
            [226] = "list",
            [231] = "long long",
            [241] = "map",
            [245] = "Matrix4x4f",
            [256] = "MdFour",
            [263] = "MonoBehaviour",
            [277] = "MonoScript",
            [288] = "m_ByteSize",
            [299] = "m_Curve",
            [307] = "m_EditorClassIdentifier",
            [331] = "m_EditorHideFlags",
            [349] = "m_Enabled",
            [359] = "m_ExtensionPtr",
            [374] = "m_GameObject",
            [387] = "m_Index",
            [395] = "m_IsArray",
            [405] = "m_IsStatic",
            [416] = "m_MetaFlag",
            [427] = "m_Name",
            [434] = "m_ObjectHideFlags",
            [452] = "m_PrefabInternal",
            [469] = "m_PrefabParentObject",
            [490] = "m_Script",
            [499] = "m_StaticEditorFlags",
            [519] = "m_Type",
            [526] = "m_Version",
            [536] = "Object",
            [543] = "pair",
            [548] = "PPtr<Component>",
            [564] = "PPtr<GameObject>",
            [581] = "PPtr<Material>",
            [596] = "PPtr<MonoBehaviour>",
            [616] = "PPtr<MonoScript>",
            [633] = "PPtr<Object>",
            [646] = "PPtr<Prefab>",
            [659] = "PPtr<Sprite>",
            [672] = "PPtr<TextAsset>",
            [688] = "PPtr<Texture>",
            [702] = "PPtr<Texture2D>",
            [718] = "PPtr<Transform>",
            [734] = "Prefab",
            [741] = "Quaternionf",
            [753] = "Rectf",
            [759] = "RectInt",
            [767] = "RectOffset",
            [778] = "second",
            [785] = "set",
            [789] = "short",
            [795] = "size",
            [800] = "SInt16",
            [807] = "SInt32",
            [814] = "SInt64",
            [821] = "SInt8",
            [827] = "staticvector",
            [840] = "string",
            [847] = "TextAsset",
            [857] = "TextMesh",
            [866] = "Texture",
            [874] = "Texture2D",
            [884] = "Transform",
            [894] = "TypelessData",
            [907] = "UInt16",
            [914] = "UInt32",
            [921] = "UInt64",
            [928] = "UInt8",
            [934] = "unsigned int",
            [947] = "unsigned long long",
            [966] = "unsigned short",
            [981] = "vector",
            [988] = "Vector2f",
            [997] = "Vector3f",
            [1006] = "Vector4f",
            [1015] = "m_FileID",
            [1024] = "m_PathID",
            [1033] = "m_ByteOffset",
            [1047] = "Gradient",
            [1056] = "Type*",
            [1062] = "int2_storage",
            [1075] = "int3_storage",
            [1088] = "BoundsInt",
            [1098] = "m_CorrespondingSourceObject",
            [1126] = "m_PrefabInstance",
            [1143] = "m_PrefabAsset",
            [1157] = "FileSize",
            [1166] = "Hash128",
        };
    }

    #endregion

    private struct TypeTreeNodeRaw
    {
        public ushort Version;
        public byte Depth;
        public byte TypeFlags;
        public int TypeStringOffset;
        public int NameStringOffset;
        public int ByteSize;
        public int Index;
        public int MetaFlags;
    }
}