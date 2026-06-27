// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Accounts;

namespace PCL.Application.Test;

[TestClass]
public sealed class LaunchProfileStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsLaunchProfiles()
    {
        using TestDirectory directory = new();
        using LaunchProfileStore store = new(
            Path.Combine(directory.Path, "profiles.json"));
        LaunchProfileSet expected = new()
        {
            Profiles =
            [
                new LaunchProfile
                {
                    Username = "Steve",
                    Info = "离线登录",
                    Kind = LaunchProfileKind.Offline,
                    Uuid = "00112233445566778899aabbccddeeff"
                },
                new LaunchProfile
                {
                    Username = "Alex",
                    Info = "第三方登录",
                    Kind = LaunchProfileKind.ThirdParty,
                    AuthServer = "https://littleskin.cn/api/yggdrasil"
                }
            ]
        };

        await store.SaveAsync(expected);
        LaunchProfileLoadResult result = await store.LoadAsync();

        Assert.IsFalse(result.WasRecovered);
        Assert.IsNull(result.BackupPath);
        Assert.AreEqual(expected.SchemaVersion, result.Profiles.SchemaVersion);
        Assert.AreEqual(2, result.Profiles.Profiles.Count);
        Assert.AreEqual(expected.Profiles[0], result.Profiles.Profiles[0]);
        Assert.AreEqual(expected.Profiles[1], result.Profiles.Profiles[1]);
    }

    [TestMethod]
    public async Task LoadAsync_InvalidJsonCreatesBackupAndReturnsEmptyProfiles()
    {
        using TestDirectory directory = new();
        string profilePath = Path.Combine(directory.Path, "profiles.json");
        await File.WriteAllTextAsync(profilePath, "{invalid");
        using LaunchProfileStore store = new(profilePath);

        LaunchProfileLoadResult result = await store.LoadAsync();

        Assert.IsTrue(result.WasRecovered);
        Assert.AreEqual(0, result.Profiles.Profiles.Count);
        Assert.IsNotNull(result.BackupPath);
        Assert.IsTrue(File.Exists(result.BackupPath));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "pcl-profiles-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
