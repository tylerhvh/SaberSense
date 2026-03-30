// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.IO;
using System.Text;

namespace SaberSense.Core.Utilities;

internal static class AtomicFileWriter
{
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        ReplaceFile(tmp, path);
    }

    public static void WriteAllBytes(string path, byte[] data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        ReplaceFile(tmp, path);
    }

    internal static void ReplaceFile(string source, string destination)
    {
        if (File.Exists(destination))
        {
            var backup = destination + ".bak";
            File.Replace(source, destination, backup);
            try { File.Delete(backup); } catch (System.Exception ex) { ModLogger.ForSource("AtomicWriter").Debug($"Backup cleanup failed: {ex.Message}"); }
        }
        else
        {
            File.Move(source, destination);
        }
    }
}