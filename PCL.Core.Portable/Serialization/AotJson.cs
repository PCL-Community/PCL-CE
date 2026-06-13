// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PCL.Core.Serialization;

/// <summary>
/// Native AOT 友好的 JSON 流式入口。调用方必须提供源生成的类型元数据。
/// </summary>
public static class AotJson
{
    public static ValueTask<T?> DeserializeAsync<T>(
        Stream utf8Json,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return JsonSerializer.DeserializeAsync(utf8Json, typeInfo, cancellationToken);
    }

    public static Task SerializeAsync<T>(
        Stream utf8Json,
        T value,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return JsonSerializer.SerializeAsync(utf8Json, value, typeInfo, cancellationToken);
    }
}
