// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Libraries;

public readonly record struct MinecraftLibraryNameFragment
{
    public MinecraftLibraryNameFragment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public bool Matches(string libraryName) =>
        libraryName.Contains(Value, StringComparison.Ordinal);

    public override string ToString() => Value;
}
