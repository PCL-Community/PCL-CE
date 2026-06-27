// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace PCL.Application.Accounts;

public sealed record LaunchProfile
{
    public required string Username { get; init; }

    public string Info { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<LaunchProfileKind>))]
    public LaunchProfileKind Kind { get; init; }

    public string Uuid { get; init; } = string.Empty;

    public string Logo { get; init; } = string.Empty;

    public string SvgIcon { get; init; } = "lucide/user";

    public string? SkinAddress { get; init; }

    public string AuthServer { get; init; } = string.Empty;
}
