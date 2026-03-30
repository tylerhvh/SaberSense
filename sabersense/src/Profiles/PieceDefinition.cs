// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Data;
using SaberSense.Core.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Profiles;

public class PieceDefinition : IDisposable
{
    public readonly LoadedBundle Asset;

    public readonly SaberSense.Behaviors.ComponentModifierRegistry ComponentModifiers;

    public SaberAssetEntry? OwnerEntry { get; set; }

    public GameObject Prefab => Asset.Prefab;

    public AuxObjectManager? AuxObjects { get; internal set; }

    public PieceOverrides? Properties { get; internal set; }

    public SaberHand AssignedHand { get; internal set; }

    public virtual Type? RendererType { get; protected set; }

    protected PieceDefinition(LoadedBundle asset, IModLogger log)
    {
        Asset = asset;
        ComponentModifiers = new SaberSense.Behaviors.ComponentModifierRegistry(asset.Prefab, log);
    }

    public virtual void Dispose() { }

    public virtual void OnCreated() { }

    public virtual void OnFirstViewed() { }

    public virtual void PersistAuxData() { }

    public virtual SaberDisplayInfo GetDisplayInfo() => default;

    public virtual void CloneStateFrom(PieceDefinition source)
    {
        if (Properties is not null && source.Properties is not null)
            Properties.CopyFrom(source.Properties);
        if (ComponentModifiers is not null && source.ComponentModifiers is not null)
            ComponentModifiers.SyncFrom(source.ComponentModifiers);
    }

    public virtual Task FromJson(JObject obj, Serializer serializer)
    {
        return Task.CompletedTask;
    }

    public virtual Task<JToken> ToJson(Serializer serializer)
    {
        var json = new JObject { { "Path", Asset.RelativePath } };
        if (!string.IsNullOrEmpty(Asset.ContentHash))
            json.Add("ContentHash", Asset.ContentHash);

        return Task.FromResult<JToken>(json);
    }
}

public sealed class PieceRegistry<T> : IEnumerable<T>
{
    private readonly List<(AssetTypeTag Tag, T Value)> _entries = new(capacity: 4);

    public int Count => _entries.Count;

    public T this[AssetTypeTag tag]
    {
        get => TryGet(tag, out var v) ? v : throw new KeyNotFoundException(tag.ToString());
        set => Register(tag, value);
    }

    public void Register(AssetTypeTag tag, T value)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Tag == tag)
            {
                _entries[i] = (tag, value);
                return;
            }
        }
        _entries.Add((tag, value));
    }

    public T? Resolve(AssetTypeTag tag)
    {
        foreach (var (t, v) in _entries)
            if (t == tag) return v;
        return default;
    }

    public bool TryGet(AssetTypeTag tag, out T value)
    {
        foreach (var (t, v) in _entries)
        {
            if (t == tag) { value = v; return true; }
        }
        value = default!;
        return false;
    }

    public bool Contains(AssetTypeTag tag)
        => _entries.Exists(e => e.Tag == tag);

    public void Clear() => _entries.Clear();

    public IEnumerator<T> GetEnumerator()
        => _entries.Select(e => e.Value).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}