// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PCL.Core.Serialization;

public static class PortableJson
{
    public static readonly JsonNodeOptions NodeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return options;
    }

    public static JsonNode ParseNode(string utf16Json)
    {
        ArgumentNullException.ThrowIfNull(utf16Json);
        return JsonNode.Parse(utf16Json, NodeOptions, DocumentOptions)!;
    }

    public static JsonObject ParseObject(string utf16Json)
    {
        return ParseNode(utf16Json).AsObject();
    }
}
