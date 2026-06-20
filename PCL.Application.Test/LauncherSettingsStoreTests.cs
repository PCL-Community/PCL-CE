// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Settings;
using PCL.Core.App;

namespace PCL.Application.Test;

[TestClass]
public sealed class LauncherSettingsStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsStronglyTypedSettings()
    {
        using TestDirectory directory = new();
        using LauncherSettingsStore store = new(
            Path.Combine(directory.Path, "settings.json"));
        LauncherSettings expected = new()
        {
            AutomaticallyRepairGameIssues = false,
            ColorMode = ColorMode.Dark,
            LightColor = ColorTheme.SkyBlue,
            DarkColor = ColorTheme.CatBlue,
            DownloadSource = DownloadSourcePreference.OfficialOnly
        };

        await store.SaveAsync(expected);
        LauncherSettingsLoadResult result = await store.LoadAsync();

        Assert.AreEqual(expected, result.Settings);
        Assert.IsFalse(result.RecoveredFromInvalidFile);
        Assert.IsNull(result.InvalidFileBackupPath);
    }

    [TestMethod]
    public async Task LoadAsync_InvalidJsonCreatesBackupAndReturnsDefaults()
    {
        using TestDirectory directory = new();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{invalid");
        using LauncherSettingsStore store = new(settingsPath);

        LauncherSettingsLoadResult result = await store.LoadAsync();

        Assert.IsTrue(result.RecoveredFromInvalidFile);
        Assert.AreEqual(new LauncherSettings(), result.Settings);
        Assert.IsNotNull(result.InvalidFileBackupPath);
        Assert.IsTrue(File.Exists(result.InvalidFileBackupPath));
    }

    [TestMethod]
    public void Normalize_DisablesUnsupportedAccentAndMirrorChoices()
    {
        LauncherSettings settings = new()
        {
            LightColor = ColorTheme.SystemAccent,
            DarkColor = ColorTheme.SystemAccent,
            DownloadSource = DownloadSourcePreference.MirrorOnly
        };

        LauncherSettings normalized = LauncherSettingsPolicy.Normalize(
            settings,
            supportsSystemAccentTheme: false,
            allowsDomesticMirror: false);

        Assert.AreEqual(ColorTheme.CatBlue, normalized.LightColor);
        Assert.AreEqual(ColorTheme.CatBlue, normalized.DarkColor);
        Assert.AreEqual(
            DownloadSourcePreference.OfficialOnly,
            normalized.DownloadSource);
        Assert.AreEqual(ColorMode.System, normalized.ColorMode);
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "pcl-settings-" + Guid.NewGuid().ToString("N"));
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
