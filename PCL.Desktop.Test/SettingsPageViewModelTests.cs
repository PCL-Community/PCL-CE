// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Settings;
using PCL.Core.App;
using PCL.Desktop.ViewModels.Settings;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class SettingsPageViewModelTests
{
    [TestMethod]
    public async Task InitializeAndSaveAsync_RoundTripsPortableSettings()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "pcl-desktop-settings-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        try
        {
            FakeThemeService theme = new();
            using SettingsPageViewModel viewModel = new(
                new LauncherSettingsStore(path),
                theme,
                new NullHintService());

            await viewModel.InitializeAsync();
            viewModel.AutomaticallyRepairGameIssues = false;
            viewModel.SelectedColorMode = viewModel.ColorModes.Single(
                static option => option.Value == ColorMode.Dark);
            viewModel.SelectedColorTheme = viewModel.ColorThemes.Single(
                static option => option.Value == ColorTheme.SkyBlue);
            viewModel.SelectedDownloadSource =
                viewModel.DownloadSources.Single(
                    static option =>
                        option.Value == DownloadSourcePreference.OfficialOnly);

            await viewModel.SaveAsync();

            using LauncherSettingsStore verificationStore = new(path);
            LauncherSettings saved =
                (await verificationStore.LoadAsync()).Settings;
            Assert.IsFalse(saved.AutomaticallyRepairGameIssues);
            Assert.AreEqual(ColorMode.Dark, saved.ColorMode);
            Assert.AreEqual(ColorTheme.SkyBlue, saved.LightColor);
            Assert.AreEqual(
                DownloadSourcePreference.OfficialOnly,
                saved.DownloadSource);
            Assert.AreEqual(ThemeMode.Dark, theme.CurrentMode);
            Assert.AreEqual(AccentColor.SkyBlue, theme.CurrentAccent);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FakeThemeService : IThemeService
    {
        public ThemeMode CurrentMode { get; private set; }

        public AccentColor CurrentAccent { get; private set; }

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public void Apply(ThemeMode mode, AccentColor accent)
        {
            CurrentMode = mode;
            CurrentAccent = accent;
            ThemeChanged?.Invoke(
                this,
                new ThemeChangedEventArgs(mode, accent));
        }
    }

    private sealed class NullHintService : IHintService
    {
        public void ShowInfo(string message)
        {
        }

        public void ShowSuccess(string message)
        {
        }

        public void ShowWarning(string message)
        {
        }

        public void ShowError(string message)
        {
        }
    }
}
