// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Java;

public sealed record JavaRuntimeDownloadFile(
    string RelativePath,
    string TargetPath,
    string Url,
    string Sha1,
    long Size);
