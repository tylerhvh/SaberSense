// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.BundleFormat;

internal static class Lz4Decoder
{
    public static byte[] Decode(byte[] compressed, int uncompressedSize)
    {
        var output = new byte[uncompressedSize];
        Decode(compressed, 0, compressed.Length, output, 0, uncompressedSize);
        return output;
    }

    public static void Decode(
        byte[] source, int srcOffset, int srcLength,
        byte[] dest, int destOffset, int destLength)
    {
        int srcEnd = srcOffset + srcLength;
        int destEnd = destOffset + destLength;
        int si = srcOffset;
        int di = destOffset;

        while (si < srcEnd && di < destEnd)
        {
            byte token = source[si++];
            int literalLength = token >> 4;
            int matchLength = token & 0x0F;

            if (literalLength == 15)
            {
                byte extra;
                do
                {
                    extra = source[si++];
                    literalLength += extra;
                } while (extra == 255);
            }

            if (si + literalLength > srcEnd)
                throw new System.IO.InvalidDataException("LZ4: truncated literal data");
            int safeLiteralLen = Math.Min(literalLength, destEnd - di);
            Buffer.BlockCopy(source, si, dest, di, safeLiteralLen);
            si += literalLength;
            di += safeLiteralLen;

            if (di >= destEnd) break;

            int matchOffset = source[si] | (source[si + 1] << 8);
            si += 2;

            matchLength += 4;
            if ((token & 0x0F) == 15)
            {
                byte extra;
                do
                {
                    extra = source[si++];
                    matchLength += extra;
                } while (extra == 255);
            }

            int matchStart = di - matchOffset;
            for (int i = 0; i < matchLength && di < destEnd; i++, di++)
            {
                dest[di] = dest[matchStart + i];
            }
        }
    }
}