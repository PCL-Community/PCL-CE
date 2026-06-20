// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using PCL.Core.App;

namespace PCL.Application.Settings;

public sealed record LauncherSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public bool AutomaticallyRepairGameIssues { get; init; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter<ColorMode>))]
    public ColorMode ColorMode { get; init; } = ColorMode.System;

    [JsonConverter(typeof(JsonStringEnumConverter<ColorTheme>))]
    public ColorTheme LightColor { get; init; } = ColorTheme.CatBlue;

    [JsonConverter(typeof(JsonStringEnumConverter<ColorTheme>))]
    public ColorTheme DarkColor { get; init; } = ColorTheme.CatBlue;

    [JsonConverter(typeof(JsonStringEnumConverter<DownloadSourcePreference>))]
    public DownloadSourcePreference DownloadSource { get; init; } =
        DownloadSourcePreference.PreferOfficialWithMirrorFallback;
}
