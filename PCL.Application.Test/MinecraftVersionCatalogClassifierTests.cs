// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftVersionCatalogClassifierTests
{
    [TestMethod]
    public void Classify_ShouldMirrorWpfMinecraftVersionCategories()
    {
        MinecraftVersionClassification release = MinecraftVersionCatalogClassifier.Classify(
            Version("1.20.1", "release", "2023-06-12T00:00:00Z"));
        MinecraftVersionClassification snapshotRelease = MinecraftVersionCatalogClassifier.Classify(
            Version("1.20.5", "snapshot", "2024-04-23T00:00:00Z"));
        MinecraftVersionClassification beforeRelease = MinecraftVersionCatalogClassifier.Classify(
            Version("b1.7.3", "old_beta", "2011-07-08T00:00:00Z"));

        Assert.AreEqual(MinecraftVersionCategory.Release, release.Category);
        Assert.AreEqual("release", release.Type);
        Assert.AreEqual(MinecraftVersionCategory.Release, snapshotRelease.Category);
        Assert.AreEqual("release", snapshotRelease.Type);
        Assert.AreEqual(MinecraftVersionCategory.BeforeRelease, beforeRelease.Category);
    }

    [TestMethod]
    public void Classify_ShouldNormalizeKnownAprilFoolsVersionsLikeWpf()
    {
        MinecraftVersionClassification infinite = MinecraftVersionCatalogClassifier.Classify(
            Version("20w14infinite", "snapshot", "2020-04-01T14:00:00Z"));
        MinecraftVersionClassification red = MinecraftVersionCatalogClassifier.Classify(
            Version("2point0_red", "snapshot", "2013-04-01T00:00:00Z"));
        MinecraftVersionClassification craftmine = MinecraftVersionCatalogClassifier.Classify(
            Version("25w14craftmine", "snapshot", "2025-04-01T00:00:00Z"));

        Assert.AreEqual(MinecraftVersionCategory.AprilFools, infinite.Category);
        Assert.AreEqual("20w14∞", infinite.Id);
        Assert.AreEqual("special", infinite.Type);
        Assert.AreEqual("Minecraft.Fool.Description.2020", infinite.AprilFoolsDescriptor?.DescriptionResourceKey);

        Assert.AreEqual("2.0_red", red.Id);
        Assert.AreEqual("Minecraft.Fool.Description.2013", red.AprilFoolsDescriptor?.DescriptionResourceKey);
        Assert.AreEqual("Minecraft.Fool.Tag.Red", red.AprilFoolsDescriptor?.TagResourceKey);

        Assert.AreEqual(MinecraftVersionCategory.AprilFools, craftmine.Category);
        Assert.AreEqual("Minecraft.Fool.Description.2025", craftmine.AprilFoolsDescriptor?.DescriptionResourceKey);
    }

    [TestMethod]
    public void Classify_ShouldNotTreatOrdinaryAprilFirstReleaseAsAprilFools()
    {
        MinecraftVersionClassification release = MinecraftVersionCatalogClassifier.Classify(
            Version("26.1.1", "release", "2026-04-01T12:00:00Z"));

        Assert.AreEqual(MinecraftVersionCategory.Release, release.Category);
        Assert.AreEqual("release", release.Type);
        Assert.IsNull(release.AprilFoolsDescriptor);
    }

    [TestMethod]
    public void FormatVersion_ShouldKeepWpfDisplayAliases()
    {
        Assert.AreEqual("Classic_0.30", MinecraftVersionCatalogClassifier.FormatVersion("c0.30_01c"));
        Assert.AreEqual("Beta_1.6_Test_Build_3", MinecraftVersionCatalogClassifier.FormatVersion("b1.6-tb3"));
        Assert.AreEqual("1.14.3_-_Combat_Test", MinecraftVersionCatalogClassifier.FormatVersion("1_14_combat-212796"));
        Assert.AreEqual("Infdev_20100630", MinecraftVersionCatalogClassifier.FormatVersion("inf-20100630-1"));
    }

    private static MinecraftVersionManifestEntry Version(string id, string type, string releaseTime) =>
        new(id, type, $"https://example.invalid/{id}.json", DateTimeOffset.Parse(releaseTime, CultureInfo.InvariantCulture));
}
