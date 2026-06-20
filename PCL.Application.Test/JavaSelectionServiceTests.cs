// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch;
using PCL.Domain.Minecraft.Java;
using PCL.Domain.Minecraft.Launch;
using PCL.Platform.Abstractions.Java;

namespace PCL.Application.Test;

[TestClass]
public sealed class JavaSelectionServiceTests
{
    [TestMethod]
    public void Resolver_ShouldRequireJava21_ForMinecraft1205OrLater()
    {
        JavaRequirementResolution resolution = JavaRuntimeRequirementResolver.Resolve(new MinecraftLaunchProfile
        {
            InstanceId = "1.20.5",
            HasReliableVanillaVersion = true,
            VanillaVersion = new Version(20, 0, 5)
        });

        Assert.IsTrue(resolution.Success);
        Assert.IsTrue(resolution.Range.Contains(new Version(21, 0, 0, 0)));
        Assert.IsFalse(resolution.Range.Contains(new Version(17, 0, 0, 0)));
    }

    [TestMethod]
    public void Resolver_ShouldConstrainOptiFine18ToJava8()
    {
        JavaRequirementResolution resolution = JavaRuntimeRequirementResolver.Resolve(new MinecraftLaunchProfile
        {
            InstanceId = "1.8.9-optifine",
            HasReliableVanillaVersion = true,
            VanillaVersion = new Version(8, 0, 9),
            HasOptiFine = true,
            ReleaseTime = new DateTimeOffset(2015, 12, 9, 0, 0, 0, TimeSpan.Zero)
        });

        Assert.IsTrue(resolution.Success);
        Assert.IsTrue(resolution.Range.Contains(new Version(1, 8, 0, 351)));
        Assert.IsFalse(resolution.Range.Contains(new Version(17, 0, 0, 0)));
        Assert.IsFalse(resolution.Range.Contains(new Version(1, 7, 0, 80)));
    }

    [TestMethod]
    public void Resolver_ShouldUseMojangJavaComponent_ForJava22OrLaterManifest()
    {
        JavaRequirementResolution resolution = JavaRuntimeRequirementResolver.Resolve(new MinecraftLaunchProfile
        {
            InstanceId = "future-snapshot",
            HasReliableVanillaVersion = false,
            ReleaseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ManifestJavaMajorVersion = 25,
            ManifestJavaComponent = "java-runtime-delta"
        });

        Assert.IsTrue(resolution.Success);
        Assert.AreEqual("java-runtime-delta", resolution.RecommendedComponent);
        Assert.IsTrue(resolution.Range.Contains(new Version(25, 0, 0, 0)));
        Assert.IsFalse(resolution.Range.Contains(new Version(21, 0, 0, 0)));
    }

    [TestMethod]
    public void Resolver_ShouldConstrainLegacyForgeToJava7()
    {
        JavaRequirementResolution resolution = JavaRuntimeRequirementResolver.Resolve(new MinecraftLaunchProfile
        {
            InstanceId = "1.6.4-forge",
            HasReliableVanillaVersion = true,
            VanillaVersion = new Version(6, 0, 4),
            HasForge = true,
            ForgeVersion = "9.11.1.1345"
        });

        Assert.IsTrue(resolution.Success);
        Assert.IsTrue(resolution.Range.Contains(new Version(1, 7, 0, 80)));
        Assert.IsFalse(resolution.Range.Contains(new Version(1, 8, 0, 202)));
    }

    [TestMethod]
    public void Resolver_ShouldConstrainLiteLoaderToJava8Maximum()
    {
        JavaRequirementResolution resolution = JavaRuntimeRequirementResolver.Resolve(new MinecraftLaunchProfile
        {
            InstanceId = "1.12.2-liteloader",
            HasReliableVanillaVersion = true,
            VanillaVersion = new Version(12, 0, 2),
            ReleaseTime = new DateTimeOffset(2017, 9, 18, 0, 0, 0, TimeSpan.Zero),
            HasLiteLoader = true
        });

        Assert.IsTrue(resolution.Success);
        Assert.IsTrue(resolution.Range.Contains(new Version(1, 8, 0, 351)));
        Assert.IsFalse(resolution.Range.Contains(new Version(11, 0, 0, 0)));
    }

    [TestMethod]
    public void Resolver_ShouldRequireJava25_ForModernCleanroom()
    {
        JavaRequirementResolution resolution = JavaRuntimeRequirementResolver.Resolve(new MinecraftLaunchProfile
        {
            InstanceId = "cleanroom-modern",
            HasCleanroom = true,
            CleanroomVersion = "0.5.1-beta"
        });

        Assert.IsTrue(resolution.Success);
        Assert.IsTrue(resolution.Range.Contains(new Version(25, 0, 0, 0)));
        Assert.IsFalse(resolution.Range.Contains(new Version(21, 0, 0, 0)));
    }

    [TestMethod]
    public async Task Selection_ShouldPreferLowestSuitableMajorVersionThenJdkAndBrand()
    {
        JavaRuntimeCandidate[] candidates =
        [
            Candidate("jdk-21-zulu", new Version(21, 0, 2), JavaBrand.Zulu, isJre: false),
            Candidate("jre-17-temurin", new Version(17, 0, 9), JavaBrand.EclipseTemurin, isJre: true),
            Candidate("jdk-17-microsoft", new Version(17, 0, 7), JavaBrand.Microsoft, isJre: false),
            Candidate("jdk-17-temurin", new Version(17, 0, 10), JavaBrand.EclipseTemurin, isJre: false)
        ];
        JavaSelectionService service = new(new InMemoryJavaLocator(candidates));

        JavaSelectionResult result = await service.SelectAsync(new MinecraftLaunchProfile
        {
            InstanceId = "1.18.2",
            HasReliableVanillaVersion = true,
            VanillaVersion = new Version(18, 0, 2)
        });

        Assert.IsTrue(result.Success);
        Assert.AreEqual("jdk-17-temurin", result.SelectedJava?.Installation.JavaHome);
    }

    [TestMethod]
    public async Task Selection_ShouldIgnoreDisabledAndUnavailableCandidates()
    {
        JavaRuntimeCandidate[] candidates =
        [
            Candidate("jdk-21-disabled", new Version(21, 0, 2), JavaBrand.EclipseTemurin, isJre: false) with { IsEnabled = false },
            Candidate("jdk-21-missing", new Version(21, 0, 1), JavaBrand.EclipseTemurin, isJre: false) with { IsAvailable = false },
            Candidate("jdk-21-usable", new Version(21, 0, 0), JavaBrand.EclipseTemurin, isJre: false)
        ];
        JavaSelectionService service = new(new InMemoryJavaLocator(candidates));

        JavaSelectionResult result = await service.SelectAsync(new MinecraftLaunchProfile
        {
            InstanceId = "1.20.5",
            HasReliableVanillaVersion = true,
            VanillaVersion = new Version(20, 0, 5)
        });

        Assert.IsTrue(result.Success);
        Assert.AreEqual("jdk-21-usable", result.SelectedJava?.Installation.JavaHome);
    }

    [TestMethod]
    public async Task Selection_ShouldReturnStructuredFailure_WhenCleanroomVersionIsInvalid()
    {
        JavaSelectionService service = new(new InMemoryJavaLocator([]));

        JavaSelectionResult result = await service.SelectAsync(new MinecraftLaunchProfile
        {
            InstanceId = "cleanroom-invalid",
            HasCleanroom = true,
            CleanroomVersion = "not-a-version"
        });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(JavaSelectionFailureReason.InvalidVersionMetadata, result.FailureReason);
        StringAssert.Contains(result.Detail, "Cleanroom");
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldAutoDownloadModernJava()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(new Version(21, 0, 0, 0), JavaVersionRange.Any.Maximum),
            recommendedComponent: null,
            hasForge: false);

        Assert.IsTrue(decision.CanAutoDownload);
        Assert.AreEqual("21", decision.JavaVersionCode);
        Assert.AreEqual("21", decision.DownloadComponent);
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldPreferRecommendedComponent()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(new Version(25, 0, 0, 0), JavaVersionRange.Any.Maximum),
            recommendedComponent: "java-runtime-delta",
            hasForge: false);

        Assert.IsTrue(decision.CanAutoDownload);
        Assert.AreEqual("25", decision.JavaVersionCode);
        Assert.AreEqual("java-runtime-delta", decision.DownloadComponent);
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldBlockLegacyJava7WithoutForge()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(JavaVersionRange.ForMajor(7), JavaVersionRange.Java7Maximum),
            recommendedComponent: null,
            hasForge: false);

        Assert.IsFalse(decision.CanAutoDownload);
        Assert.AreEqual(JavaAcquisitionBlockReason.LegacyJava7Required, decision.BlockReason);
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldBlockLegacyForgeWithSpecificReason()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(JavaVersionRange.ForMajor(7), JavaVersionRange.Java7Maximum),
            recommendedComponent: null,
            hasForge: true);

        Assert.IsFalse(decision.CanAutoDownload);
        Assert.AreEqual(JavaAcquisitionBlockReason.LegacyForgeNeedsFixerOrJava7, decision.BlockReason);
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldBlockNarrowJava8UpdateRange()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(new Version(1, 8, 0, 141), new Version(1, 8, 0, 320)),
            recommendedComponent: null,
            hasForge: false);

        Assert.IsFalse(decision.CanAutoDownload);
        Assert.AreEqual(JavaAcquisitionBlockReason.Java8Update141To320Required, decision.BlockReason);
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldBlockJava8Update141OrLater()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(new Version(1, 8, 0, 141), JavaVersionRange.Java8Maximum),
            recommendedComponent: null,
            hasForge: false);

        Assert.IsFalse(decision.CanAutoDownload);
        Assert.AreEqual(JavaAcquisitionBlockReason.Java8Update141OrLaterRequired, decision.BlockReason);
    }

    [TestMethod]
    public void AcquisitionPlanner_ShouldAutoDownloadJava8ForBroadJava8Range()
    {
        JavaRuntimeAcquisitionDecision decision = JavaRuntimeAcquisitionPlanner.Plan(
            new JavaVersionRange(JavaVersionRange.ForMajor(8), JavaVersionRange.Java8Maximum),
            recommendedComponent: null,
            hasForge: false);

        Assert.IsTrue(decision.CanAutoDownload);
        Assert.AreEqual("8", decision.JavaVersionCode);
        Assert.AreEqual("8", decision.DownloadComponent);
    }

    private static JavaRuntimeCandidate Candidate(
        string javaHome,
        Version version,
        JavaBrand brand,
        bool isJre) =>
        new(
            new JavaInstallation(
                javaHome,
                Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"),
                OperatingSystem.IsWindows() ? Path.Combine(javaHome, "bin", "javaw.exe") : null,
                version,
                brand,
                JavaArchitecture.X64,
                Is64Bit: true,
                isJre));

    private sealed class InMemoryJavaLocator(IReadOnlyList<JavaRuntimeCandidate> candidates) : IJavaLocator
    {
        public ValueTask<IReadOnlyList<JavaRuntimeCandidate>> FindAllAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(candidates);
    }
}
