// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public static class YggdrasilScopeRegistry
{
    private static readonly YggdrasilScope[] Required =
    [
        YggdrasilScope.OpenId,
        YggdrasilScope.PlayerProfilesSelect,
        YggdrasilScope.ServerJoin
    ];

    public static ReadOnlySpan<YggdrasilScope> RequiredScopes => Required;
}
