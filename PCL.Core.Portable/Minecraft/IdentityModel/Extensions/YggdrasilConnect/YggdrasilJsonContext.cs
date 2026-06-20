// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(YggdrasilConnectMetaData))]
internal sealed partial class YggdrasilJsonContext : JsonSerializerContext;
