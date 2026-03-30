// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace SaberSense.Core.Utilities;

internal static class JObjectExtensions
{
    public static bool TryGetObject(this JObject obj, string key, [NotNullWhen(true)] out JObject? result)
    {
        if (obj.TryGetValue(key, out var token) && token is JObject jObj)
        {
            result = jObj;
            return true;
        }
        result = null;
        return false;
    }
}