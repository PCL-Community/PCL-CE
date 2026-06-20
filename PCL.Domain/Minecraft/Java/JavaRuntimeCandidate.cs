// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Domain.Minecraft.Java;

public sealed record JavaRuntimeCandidate(
    JavaInstallation Installation,
    bool IsEnabled = true,
    bool IsAvailable = true,
    JavaSource Source = JavaSource.AutoScanned)
{
    public override string ToString() =>
        $"{(IsEnabled ? "[enabled]" : "[disabled]")} {Installation}";
}
