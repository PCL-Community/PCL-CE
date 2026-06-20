// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public sealed record YggdrasilConnectMetaData : OpenIdMetadata
{
    [JsonPropertyName("shared_client_id")]
    public string? SharedClientId { get; init; }
}
