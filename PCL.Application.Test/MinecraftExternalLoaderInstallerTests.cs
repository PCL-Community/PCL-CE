// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftExternalLoaderInstallerTests
{
    [TestMethod]
    public void ArtifactResolver_UsesLoaderSpecificOfficialCoordinates()
    {
        MinecraftLoaderInstallerArtifact forge = MinecraftLoaderInstallerArtifactResolver.Resolve(
            MinecraftLoaderKind.Forge,
            "1.20.1",
            "47.2.0");
        MinecraftLoaderInstallerArtifact neoForge = MinecraftLoaderInstallerArtifactResolver.Resolve(
            MinecraftLoaderKind.NeoForge,
            "1.21.1",
            "21.1.80");
        MinecraftLoaderInstallerArtifact cleanroom = MinecraftLoaderInstallerArtifactResolver.Resolve(
            MinecraftLoaderKind.Cleanroom,
            "1.12.2",
            "0.5.15-alpha");
        MinecraftLoaderInstallerArtifact optiFine = MinecraftLoaderInstallerArtifactResolver.Resolve(
            MinecraftLoaderKind.OptiFine,
            "1.20.1",
            "1.20.1_HD_U_I6");

        StringAssert.Contains(forge.Sources[0], "/1.20.1-47.2.0/forge-1.20.1-47.2.0-installer.jar");
        StringAssert.Contains(neoForge.Sources[0], "/neoforge/21.1.80/neoforge-21.1.80-installer.jar");
        StringAssert.Contains(cleanroom.Sources[0], "/0.5.15-alpha/cleanroom-0.5.15-alpha-installer.jar");
        StringAssert.EndsWith(optiFine.Sources[0], "/optifine/1.20.1/HD_U/I6");
    }

    [TestMethod]
    public void ArtifactResolver_UsesLegacyNeoForgeArtifactFor1201()
    {
        MinecraftLoaderInstallerArtifact artifact = MinecraftLoaderInstallerArtifactResolver.Resolve(
            MinecraftLoaderKind.NeoForge,
            "1.20.1",
            "47.1.99");

        StringAssert.Contains(artifact.Sources[0], "/net/neoforged/forge/1.20.1-47.1.99/forge-1.20.1-47.1.99-installer.jar");
    }
}
