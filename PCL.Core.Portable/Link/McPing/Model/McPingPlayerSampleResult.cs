// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace PCL.Core.Link.McPing.Model;

public record McPingPlayerSampleResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("id")] string Id);
