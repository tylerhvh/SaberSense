// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.Core.Utilities;

public static class FileIO
{
    private const long MaxFileSize = 200 * 1024 * 1024;

    public static async Task<byte[]> SlurpAsync(string path)
    {
        await using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 8192, useAsync: true);

        if (fs.Length > MaxFileSize)
            throw new IOException($"File too large ({fs.Length / (1024 * 1024)}MB > {MaxFileSize / (1024 * 1024)}MB limit): {path}");

        var buffer = new byte[fs.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await fs.ReadAsync(
                buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead is 0) break;
            totalRead += bytesRead;
        }
        return buffer;
    }

    public static async Task<byte[]> DrainAsync(this Stream source)
    {
        var buffer = new byte[source.Length];
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = await source.ReadAsync(buffer, offset, buffer.Length - offset);
            if (n is 0) break;
            offset += n;
        }
        return buffer;
    }

    public static async Task<byte[]> EmbeddedResourceAsync(string resourceKey)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream(resourceKey);

        if (stream is null)
        {
            var match = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceKey, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                stream = asm.GetManifestResourceStream(match);
        }

        if (stream is null) return [];

        await using (stream)
            return await stream.DrainAsync();
    }
}

public static class BundleLoader
{
    public readonly record struct BundleResult<T>(T Asset, AssetBundle Bundle) where T : Object;

    public static async Task<AssetBundle> FromMemoryAsync(byte[] data)
    {
        var request = AssetBundle.LoadFromMemoryAsync(data);
        await request;
        return request.assetBundle;
    }

    public static async Task<T> ExtractAssetAsync<T>(
        this AssetBundle bundle, string assetName) where T : Object
    {
        var request = bundle.LoadAssetAsync<T>(assetName);
        await request;
        return (request.asset as T)!;
    }

    public static async Task<BundleResult<T>?> LoadFromBytesAsync<T>(
        byte[] bundleData, string assetName) where T : Object
    {
        var bundle = await FromMemoryAsync(bundleData);
        if (bundle == null) return null;

        var asset = await bundle.ExtractAssetAsync<T>(assetName);
        if (asset == null)
        {
            bundle.Unload(true);
            return null;
        }

        return new BundleResult<T>(asset, bundle);
    }

    public static event System.Action<int, int>? OnAssetProgress;

    public static async Task<BundleResult<T>?> LoadFromFileAsync<T>(
        string filePath, string assetName) where T : Object
    {
        var bundleRequest = AssetBundle.LoadFromFileAsync(filePath);
        while (!bundleRequest.isDone)
            await Task.Yield();

        var bundle = bundleRequest.assetBundle;
        if (bundle == null) return null;

        var allNames = bundle.GetAllAssetNames();
        for (int i = 0; i < allNames.Length; i++)
        {
            var req = bundle.LoadAssetAsync(allNames[i]);
            while (!req.isDone)
                await Task.Yield();
            OnAssetProgress?.Invoke(i + 1, allNames.Length);
        }

        var assetRequest = bundle.LoadAssetAsync<T>(assetName);
        while (!assetRequest.isDone)
            await Task.Yield();

        var asset = assetRequest.asset as T;
        if (asset == null)
        {
            bundle.Unload(true);
            return null;
        }

        return new BundleResult<T>(asset, bundle);
    }
}

public static class UnityAsyncExt
{
    public static System.Runtime.CompilerServices.TaskAwaiter GetAwaiter(
        this AsyncOperation op)
    {
        var tcs = new TaskCompletionSource<object?>();
        if (op.isDone)
        {
            tcs.TrySetResult(null);
        }
        else
        {
            op.completed += _ => tcs.TrySetResult(null);
        }
        return ((Task)tcs.Task).GetAwaiter();
    }
}