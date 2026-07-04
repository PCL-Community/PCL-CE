// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Instances;

public sealed record InstanceMetadata
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Description { get; init; } = string.Empty;

    public int LaunchCount { get; init; }

    public string ModpackVersion { get; init; } = string.Empty;

    public string ModpackProjectId { get; init; } = string.Empty;

    public bool IsStarred { get; init; }

    public string LogoPath { get; init; } = string.Empty;

    public int CardType { get; init; }

    public bool DisableAssetVerification { get; init; }

    public bool InstanceIsolation { get; init; } = true;

    public string WindowTitle { get; init; } = string.Empty;

    public bool UseGlobalWindowTitle { get; init; } = true;

    public string CustomInfo { get; init; } = string.Empty;

    public int MemorySolution { get; init; } = 2;

    public int CustomMemorySize { get; init; } = 13;

    public int ServerLoginRequirement { get; init; }

    public string AuthServerAddress { get; init; } = string.Empty;

    public string AuthRegisterAddress { get; init; } = string.Empty;

    public string AuthServerDisplayName { get; init; } = string.Empty;

    public string ServerToEnter { get; init; } = string.Empty;

    public int Renderer { get; init; }

    public string JvmArguments { get; init; } = string.Empty;

    public string GameArguments { get; init; } = string.Empty;

    public string ClasspathHead { get; init; } = string.Empty;

    public string PreLaunchCommand { get; init; } = string.Empty;

    public bool WaitForPreLaunchCommand { get; init; }

    public bool IgnoreJavaCompatibility { get; init; }

    public bool UseProxy { get; init; }

    public bool DisableJlw { get; init; }

    public bool DisableRw { get; init; }

    public bool UseDebugLog4j2Config { get; init; }

    public bool DisableLwjglUnsafeAgent { get; init; }
}
