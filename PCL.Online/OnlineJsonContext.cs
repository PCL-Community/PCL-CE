// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PCL.Core.Serialization;

namespace PCL.Online;

[JsonSerializable(typeof(ClientRegionPolicy))]
[JsonSerializable(typeof(CloudSyncMetadataFile))]
[JsonSerializable(typeof(CloudUserDocument))]
[JsonSerializable(typeof(CloudUserSyncRequest))]
[JsonSerializable(typeof(FriendRequestCreate))]
[JsonSerializable(typeof(PresenceRequest))]
[JsonSerializable(typeof(PresenceResponse))]
[JsonSerializable(typeof(List<OnlineFriendRequest>), TypeInfoPropertyName = "OnlineFriendRequestList")]
[JsonSerializable(typeof(Dictionary<string, CloudSyncSectionMetadata>), TypeInfoPropertyName = "CloudSyncSectionMetadataDictionary")]
[JsonSerializable(typeof(string[][]), TypeInfoPropertyName = "StringArrayArray")]
internal sealed partial class OnlineJsonContext : JsonSerializerContext;

internal static class OnlineJson
{
    private static readonly OnlineJsonContext DefaultContext = new(PortableJson.SerializerOptions);

    public static string Serialize<T>(T value, bool writeIndented = false)
    {
        OnlineJsonContext context = writeIndented
            ? new OnlineJsonContext(new JsonSerializerOptions(PortableJson.SerializerOptions)
            {
                WriteIndented = true
            })
            : DefaultContext;
        return JsonSerializer.Serialize(value, GetTypeInfo<T>(context));
    }

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize(json, GetTypeInfo<T>(DefaultContext));

    public static JsonTypeInfo<T> TypeInfo<T>() => GetTypeInfo<T>(DefaultContext);

    private static JsonTypeInfo<T> GetTypeInfo<T>(OnlineJsonContext context) =>
        context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new NotSupportedException($"JSON type '{typeof(T)}' is not registered.");
}
