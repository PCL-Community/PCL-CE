// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Launching.Views;

public sealed record LoginProfileInfo(
    string Username,
    string Info,
    LaunchLoginProfileKind Kind,
    string Uuid = "",
    string Logo = "",
    string SvgIcon = "lucide/user",
    string? SkinAddress = null,
    string AuthServer = "",
    string AccessToken = "",
    string RefreshToken = "")
{
    public bool HasSkin => !string.IsNullOrWhiteSpace(SkinAddress);
}

public enum LaunchLoginProfileKind
{
    Microsoft,
    ThirdParty,
    Offline
}
