// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Domain.Minecraft.Java;

public sealed record JavaInstallation(
    string JavaHome,
    string JavaExecutablePath,
    string? WindowedJavaExecutablePath,
    Version Version,
    JavaBrand Brand,
    JavaArchitecture Architecture,
    bool Is64Bit,
    bool IsJre)
{
    public int MajorVersion => Version.Major == 1 ? Version.Minor : Version.Major;

    public override string ToString() =>
        $"{(IsJre ? "JRE" : "JDK")} {MajorVersion} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaHome}";

    public string ToDetailedString() =>
        $"{(IsJre ? "JRE" : "JDK")} {Version} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaHome}";
}
