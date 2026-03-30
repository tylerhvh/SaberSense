// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace SaberSense.Core.BundleFormat;

internal static class BundleReader
{
    private const int CompressionNone = 0;
    private const int CompressionLzma = 1;
    private const int CompressionLz4 = 2;
    private const int CompressionLz4Hc = 3;

    public static Dictionary<string, byte[]> ExtractBundleContent(string bundlePath)
    {
        using var stream = File.OpenRead(bundlePath);
        using var reader = new EndianReader(stream, bigEndian: true);

        var signature = reader.ReadNullTerminated();
        if (signature != "UnityFS")
            throw new InvalidDataException($"Expected 'UnityFS' signature, got '{signature}'");

        var formatVersion = reader.ReadInt32();
        var unityVersion = reader.ReadNullTerminated();
        var generatorVersion = reader.ReadNullTerminated();

        var bundleSize = reader.ReadInt64();
        var compressedBlockInfoSize = reader.ReadInt32();
        var uncompressedBlockInfoSize = reader.ReadInt32();
        var flags = reader.ReadInt32();

        int compressionType = flags & 0x3F;
        bool blockInfoAtEnd = (flags & 0x80) != 0;

        ModLogger.ForSource("Bundle").Debug($"'{Path.GetFileName(bundlePath)}' fmt={formatVersion} " +
                             $"gen={generatorVersion} size={bundleSize} cbis={compressedBlockInfoSize} " +
                             $"ubis={uncompressedBlockInfoSize} flags=0x{flags:X} headerPos={reader.Position}");

        long headerEnd = reader.Position;
        if (formatVersion >= 7)
        {
            headerEnd = (headerEnd + 15) & ~15L;
            reader.Position = headerEnd;
        }

        long dataBlocksStart;
        byte[] blockInfoData;

        if (blockInfoAtEnd)
        {
            dataBlocksStart = headerEnd;
            long savedPos = reader.Position;

            reader.Position = bundleSize - compressedBlockInfoSize;
            var compressedBlockInfo = reader.ReadBytes(compressedBlockInfoSize);

            blockInfoData = DecompressData(compressedBlockInfo, compressionType,
                                           uncompressedBlockInfoSize, "block info");

            reader.Position = savedPos;
        }
        else
        {
            var compressedBlockInfo = reader.ReadBytes(compressedBlockInfoSize);

            blockInfoData = DecompressData(compressedBlockInfo, compressionType,
                                           uncompressedBlockInfoSize, "block info");

            dataBlocksStart = reader.Position;
        }

        using var blockInfoStream = new MemoryStream(blockInfoData);
        using var blockReader = new EndianReader(blockInfoStream, bigEndian: true);

        blockReader.ReadBytes(16);
        int blockCount = blockReader.ReadInt32();

        var blocks = new BlockInfo[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            blocks[i] = new BlockInfo
            {
                UncompressedSize = blockReader.ReadInt32(),
                CompressedSize = blockReader.ReadInt32(),
                Flags = blockReader.ReadUInt16()
            };
        }

        int directoryCount = blockReader.ReadInt32();
        var directories = new DirectoryEntry[directoryCount];
        for (int i = 0; i < directoryCount; i++)
        {
            directories[i] = new DirectoryEntry
            {
                Offset = blockReader.ReadInt64(),
                Size = blockReader.ReadInt64(),
                Flags = blockReader.ReadInt32(),
                Name = blockReader.ReadNullTerminated()
            };
        }

        ModLogger.ForSource("Bundle").Debug($"blocks={blockCount} dirs={directoryCount} dataStart={dataBlocksStart}");

        reader.Position = dataBlocksStart;
        using var decompressedStream = new MemoryStream();

        for (int i = 0; i < blockCount; i++)
        {
            int blockCompression = blocks[i].Flags & 0x3F;
            var compressedData = reader.ReadBytes(blocks[i].CompressedSize);

            var decompressed = DecompressData(compressedData, blockCompression,
                                              blocks[i].UncompressedSize, $"block {i}");
            decompressedStream.Write(decompressed, 0, decompressed.Length);
        }

        var allData = decompressedStream.ToArray();
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in directories)
        {
            if (entry.Offset + entry.Size > allData.Length)
            {
                ModLogger.ForSource("Bundle").Warn($"Directory entry '{entry.Name}' exceeds data bounds!");
                continue;
            }

            var fileData = new byte[entry.Size];
            Buffer.BlockCopy(allData, (int)entry.Offset, fileData, 0, (int)entry.Size);
            result[entry.Name] = fileData;
        }

        return result;
    }

    private static byte[] DecompressData(byte[] compressed, int compressionType,
                                          int uncompressedSize, string context)
    {
        return compressionType switch
        {
            CompressionNone => compressed,
            CompressionLzma => LzmaDecoder.Decode(compressed, uncompressedSize),
            CompressionLz4 or CompressionLz4Hc => Lz4Decoder.Decode(compressed, uncompressedSize),
            _ => throw new NotSupportedException(
                $"Unsupported compression type {compressionType} in {context}")
        };
    }

    private struct BlockInfo
    {
        public int UncompressedSize;
        public int CompressedSize;
        public ushort Flags;
    }

    private struct DirectoryEntry
    {
        public long Offset;
        public long Size;
        public int Flags;
        public string Name;
    }
}