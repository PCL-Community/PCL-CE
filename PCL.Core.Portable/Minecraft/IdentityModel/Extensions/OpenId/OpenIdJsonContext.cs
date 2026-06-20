// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(OpenIdMetadata))]
[JsonSerializable(typeof(JsonWebKeys))]
internal sealed partial class OpenIdJsonContext : JsonSerializerContext;
