// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Natives;

public sealed class MinecraftNativeArchiveException : IOException
{
    public MinecraftNativeArchiveException(string archivePath, Exception innerException)
        : base($"Unable to read native archive '{archivePath}'.", innerException)
    {
        ArchivePath = archivePath;
    }

    public string ArchivePath { get; }
}
