// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Minecraft.IdentityModel.OAuth;

public record EndpointMeta
{
    /// <summary>
    /// 设备授权端点
    /// </summary>
    public string? DeviceEndpoint { get; set; }
    /// <summary>
    /// 授权端点
    /// </summary>
    public required string AuthorizeEndpoint { get; set; }
    /// <summary>
    /// 令牌端点
    /// </summary>
    public required string TokenEndpoint { get; set; }
}
