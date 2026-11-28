using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.OS;
using PCL.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
[OSCondition(OperatingSystems.Linux)]
[DoNotParallelize]
public sealed class LinuxShortcutServiceTest
{
    private static readonly string[] Variables =
    [
        "XDG_CONFIG_HOME",
        "XDG_DESKTOP_DIR",
        "XDG_CURRENT_DESKTOP",
        "GIO_LOG",
        "PATH"
    ];

    private readonly Dictionary<string, string?> _original = [];
    private string _binRoot = string.Empty;
    private string _configHome = string.Empty;
    private string _desktopDirectory = string.Empty;
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        foreach (var variable in Variables)
            _original[variable] = Environment.GetEnvironmentVariable(variable);

        _root = Path.Combine(
            AppContext.BaseDirectory,
            $"linux-shortcut-test-{Guid.NewGuid().ToString("N")[..8]}");
        _configHome = Path.Combine(_root, "config");
        _desktopDirectory = Path.Combine(_root, "desktop");
        _binRoot = Path.Combine(_root, "bin");
        Directory.CreateDirectory(_configHome);
        Directory.CreateDirectory(_desktopDirectory);
        Directory.CreateDirectory(_binRoot);

        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configHome);
        Environment.SetEnvironmentVariable("XDG_DESKTOP_DIR", null);
        Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", null);
        Environment.SetEnvironmentVariable("GIO_LOG", null);
        Environment.SetEnvironmentVariable("PATH", _binRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var variable in Variables)
            Environment.SetEnvironmentVariable(variable, _original[variable]);

        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch
        {
            // ignored
        }
    }

    [TestMethod]
    public void GetDesktopPathUsesUserDirs()
    {
        File.WriteAllText(
            Path.Combine(_configHome, "user-dirs.dirs"),
            "XDG_DESKTOP_DIR=\"$HOME/桌面\"\n");
        var service = new LinuxShortcutService();

        Assert.AreEqual(
            Path.Combine(XdgPaths.Home, "桌面", "PCL CE.desktop"),
            service.GetDesktopPath("PCL CE.desktop"));
    }

    [TestMethod]
    public void CreateWritesCompleteDesktopEntry()
    {
        Environment.SetEnvironmentVariable("XDG_DESKTOP_DIR", _desktopDirectory);
        Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", "KDE");
        var service = new LinuxShortcutService();
        var target = Path.Combine(_root, "PCL Launcher");
        var icon = Path.Combine(_root, "icon.png");
        var shortcut = service.GetDesktopPath("PCL CE.desktop");

        service.Create(
            shortcut,
            target,
            workingDirectory: _root,
            description: "PCL CE",
            icon: icon);

        var content = File.ReadAllText(shortcut);
        Assert.Contains("[Desktop Entry]", content);
        Assert.Contains("Version=1.0", content);
        Assert.Contains("Type=Application", content);
        Assert.Contains("Name=PCL CE", content);
        Assert.Contains("Comment=PCL CE", content);
        Assert.Contains($"Exec=\"{target}\"", content);
        Assert.Contains($"TryExec={target}", content);
        Assert.Contains($"Path={_root}", content);
        Assert.Contains($"Icon={icon}", content);
        Assert.Contains("Terminal=false", content);
        Assert.Contains("Categories=Game;Utility;", content);
        Assert.Contains("StartupWMClass=pcl-community-edition", content);
        Assert.Contains("StartupNotify=true", content);
        Assert.IsTrue(
            File.GetUnixFileMode(shortcut).HasFlag(UnixFileMode.UserExecute));
    }

    [TestMethod]
    public void CreateMarksGnomeDesktopEntryTrusted()
    {
        Environment.SetEnvironmentVariable("XDG_DESKTOP_DIR", _desktopDirectory);
        Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", "GNOME");
        var log = Path.Combine(_root, "gio.log");
        Environment.SetEnvironmentVariable("GIO_LOG", log);
        var gio = Path.Combine(_binRoot, "gio");
        File.WriteAllText(gio, "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$GIO_LOG\"\n");
        File.SetUnixFileMode(
            gio,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);

        var service = new LinuxShortcutService();
        var shortcut = service.GetDesktopPath("PCL CE.desktop");
        service.Create(
            shortcut,
            Path.Combine(_root, "PCL Launcher"),
            workingDirectory: _root,
            description: "PCL CE");

        var lines = File.ReadAllLines(log);
        Assert.Contains("set", lines);
        Assert.Contains(shortcut, lines);
        Assert.Contains("metadata::trusted", lines);
        Assert.Contains("true", lines);
    }
}