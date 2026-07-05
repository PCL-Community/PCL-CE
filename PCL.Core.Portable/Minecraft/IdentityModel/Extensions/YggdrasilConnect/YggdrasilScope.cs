// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public readonly record struct YggdrasilScope
{
    public YggdrasilScope(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public static YggdrasilScope OpenId { get; } = new("openid");

    public static YggdrasilScope PlayerProfilesSelect { get; } =
        new("Yggdrasil.PlayerProfiles.Select");

    public static YggdrasilScope ServerJoin { get; } =
        new("Yggdrasil.Server.Join");

    public override string ToString() => Value;
}
