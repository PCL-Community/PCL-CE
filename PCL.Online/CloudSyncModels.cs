// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace PCL.Online;

internal sealed class CloudSyncMetadataFile
{
    public string MsId { get; set; } = "";

    public Dictionary<string, CloudSyncSectionMetadata> Sections { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class CloudSyncSectionMetadata
{
    public string Hash { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class CloudSyncSection
{
    public JsonNode? Data { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class CloudUserSyncRequest
{
    public CloudSyncSection? Account { get; set; }
    public CloudSyncSection? Favorites { get; set; }
    public CloudSyncSection? UiPreferences { get; set; }
    public CloudSyncSection? HintPreferences { get; set; }
    public CloudSyncSection? DownloadPreferences { get; set; }
    public CloudSyncSection? LaunchPreferences { get; set; }
    public CloudSyncSection? HomepagePreferences { get; set; }
    public CloudSyncSection? MusicPreferences { get; set; }
    public CloudSyncSection? UpdatePreferences { get; set; }
    public CloudSyncSection? CustomVariables { get; set; }
}

internal sealed class CloudUserDocument
{
    public string MsId { get; set; } = "";
    public CloudSyncSection? Account { get; set; }
    public CloudSyncSection? Favorites { get; set; }
    public CloudSyncSection? UiPreferences { get; set; }
    public CloudSyncSection? HintPreferences { get; set; }
    public CloudSyncSection? DownloadPreferences { get; set; }
    public CloudSyncSection? LaunchPreferences { get; set; }
    public CloudSyncSection? HomepagePreferences { get; set; }
    public CloudSyncSection? MusicPreferences { get; set; }
    public CloudSyncSection? UpdatePreferences { get; set; }
    public CloudSyncSection? CustomVariables { get; set; }
}
