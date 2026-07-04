// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Instances;

namespace PCL.Application.Test;

[TestClass]
public sealed class InstanceMetadataStoreTests
{
    [TestMethod]
    public async Task LoadAsync_MissingFileReturnsDefaultMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-instance-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            InstanceMetadata metadata = await InstanceMetadataStore.LoadAsync(root);

            Assert.AreEqual(string.Empty, metadata.Description);
            Assert.IsFalse(metadata.IsStarred);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsync_RoundTripsMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-instance-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await InstanceMetadataStore.SaveAsync(
                root,
                new InstanceMetadata
                {
                    Description = "Survival profile",
                    LaunchCount = 5,
                    ModpackVersion = "1.4.2",
                    ModpackProjectId = "modrinth-project-id",
                    IsStarred = true,
                    DisableAssetVerification = true
                });

            InstanceMetadata metadata = await InstanceMetadataStore.LoadAsync(root);

            Assert.AreEqual("Survival profile", metadata.Description);
            Assert.AreEqual(5, metadata.LaunchCount);
            Assert.AreEqual("1.4.2", metadata.ModpackVersion);
            Assert.AreEqual("modrinth-project-id", metadata.ModpackProjectId);
            Assert.IsTrue(metadata.IsStarred);
            Assert.IsTrue(metadata.DisableAssetVerification);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
