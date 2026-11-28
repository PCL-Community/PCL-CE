using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Platform;

namespace PCL.Core.Test.IO;

[TestClass]
[OSCondition(OperatingSystems.Linux)]
[DoNotParallelize]
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class RecycleBinTest
{
    private readonly IRecycleBinService _bin = new ServiceCollection()
        .AddPlatformServices(PlatformDetector.Detect())
        .BuildServiceProvider()
        .GetRequiredService<IRecycleBinService>();

    private string _binRoot = string.Empty;
    private string? _originalDataHome;
    private string? _originalGioDest;
    private string? _originalGioLog;
    private string? _originalPath;
    private string _sandboxRoot = string.Empty;
    private string _trashRoot = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _originalDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        _originalPath = Environment.GetEnvironmentVariable("PATH");
        _originalGioDest = Environment.GetEnvironmentVariable("GIO_DEST");
        _originalGioLog = Environment.GetEnvironmentVariable("GIO_LOG");
        _sandboxRoot = Path.Combine(
            AppContext.BaseDirectory,
            $"recycle-bin-test-{Guid.NewGuid().ToString("N")[..8]}");
        _trashRoot = Path.Combine(_sandboxRoot, "xdg-data");
        _binRoot = Path.Combine(_sandboxRoot, "bin");
        Directory.CreateDirectory(_sandboxRoot);
        Directory.CreateDirectory(_binRoot);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _trashRoot);
        Environment.SetEnvironmentVariable("PATH", _binRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _originalDataHome);
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        Environment.SetEnvironmentVariable("GIO_DEST", _originalGioDest);
        Environment.SetEnvironmentVariable("GIO_LOG", _originalGioLog);
        try
        {
            if (Directory.Exists(_sandboxRoot)) Directory.Delete(_sandboxRoot, true);
        }
        catch
        {
            // ignored
        }
    }

    [TestMethod]
    public void IsSupported_ReturnsFalseForMissingPath()
    {
        Assert.IsFalse(_bin.IsSupported(Path.Combine(_sandboxRoot, "missing")));
        Assert.IsFalse(_bin.IsSupported(null));
    }

    [TestMethod]
    public void Move_Directory_ProducesRestorableTrashEntry()
    {
        var source = Path.Combine(_sandboxRoot, "instance-under-test");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "options.txt"), "fov:0.0");

        Assert.IsTrue(_bin.IsSupported(source), "工作区位于用户主目录下，应支持回收站");

        var outcome = _bin.Move(source, out var error);

        Assert.AreEqual(RecycleBinOutcome.Moved, outcome, error);
        Assert.IsFalse(Directory.Exists(source), "原目录应已不在原位");

        var trashed = Path.Combine(_trashRoot, "Trash", "files", "instance-under-test");
        Assert.IsTrue(Directory.Exists(trashed), "目录应出现在回收站 files/ 下");
        Assert.IsTrue(File.Exists(Path.Combine(trashed, "options.txt")), "回收站内应保留原内容");

        var info = Path.Combine(_trashRoot, "Trash", "info", "instance-under-test.trashinfo");
        Assert.IsTrue(File.Exists(info), "应写入 .trashinfo 以支持桌面环境还原");
        var text = File.ReadAllText(info);
        Assert.Contains("[Trash Info]", text);
        Assert.Contains("Path=", text);
        Assert.Contains("DeletionDate=", text);
    }

    [TestMethod]
    public void Move_File_ProducesTrashEntry()
    {
        var source = Path.Combine(_sandboxRoot, "world.zip");
        File.WriteAllText(source, "zip-bytes");

        var outcome = _bin.Move(source, out var error);

        Assert.AreEqual(RecycleBinOutcome.Moved, outcome, error);
        Assert.IsFalse(File.Exists(source));
        Assert.IsTrue(File.Exists(Path.Combine(_trashRoot, "Trash", "files", "world.zip")));
    }

    [TestMethod]
    public void Move_RepeatedName_GetsSuffixInsteadOfOverwriting()
    {
        var first = Path.Combine(_sandboxRoot, "saves");
        Directory.CreateDirectory(first);
        Assert.AreEqual(RecycleBinOutcome.Moved, _bin.Move(first, out _));

        var second = Path.Combine(_sandboxRoot, "saves");
        Directory.CreateDirectory(second);
        Assert.AreEqual(RecycleBinOutcome.Moved, _bin.Move(second, out _));

        var files = Path.Combine(_trashRoot, "Trash", "files");
        Assert.IsTrue(Directory.Exists(Path.Combine(files, "saves")));
        Assert.IsTrue(Directory.Exists(Path.Combine(files, "saves.2")), "重名条目应加后缀，不能覆盖已有回收站内容");
    }

    [TestMethod]
    public void Move_MissingPath_ReportsFailure()
    {
        var outcome = _bin.Move(Path.Combine(_sandboxRoot, "not-there"), out var error);
        Assert.AreEqual(RecycleBinOutcome.Failed, outcome);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    [TestMethod]
    public void Move_PrefersGioTrash()
    {
        var gio = Path.Combine(_binRoot, "gio");
        File.WriteAllText(
            gio,
            "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$GIO_LOG\"\nmv \"$3\" \"$GIO_DEST\"\n");
        File.SetUnixFileMode(
            gio,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
        var destination = Path.Combine(_sandboxRoot, "gio-dest");
        Directory.CreateDirectory(destination);
        var log = Path.Combine(_sandboxRoot, "gio.log");
        Environment.SetEnvironmentVariable("GIO_DEST", destination);
        Environment.SetEnvironmentVariable("GIO_LOG", log);
        Environment.SetEnvironmentVariable(
            "PATH",
            string.IsNullOrEmpty(_originalPath)
                ? _binRoot
                : _binRoot + Path.PathSeparator + _originalPath);

        var source = Path.Combine(_sandboxRoot, "via-gio.txt");
        File.WriteAllText(source, "gio-bytes");

        var outcome = _bin.Move(source, out var error);

        Assert.AreEqual(RecycleBinOutcome.Moved, outcome, error);
        Assert.IsTrue(File.Exists(log), "假 gio 没有被调用");
        Assert.IsFalse(File.Exists(source));
        Assert.IsTrue(File.Exists(Path.Combine(destination, "via-gio.txt")));
        Assert.IsFalse(
            Directory.Exists(Path.Combine(_trashRoot, "Trash", "files")),
            "gio 成功时不应再走原生回收站");
        Assert.Contains("trash", File.ReadAllLines(log));
        Assert.Contains("--", File.ReadAllLines(log));
        Assert.Contains(source, File.ReadAllLines(log));
    }
}