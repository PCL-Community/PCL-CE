using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public class RecycleBinTest
{
    private string? _originalDataHome;
    private string _sandboxRoot = string.Empty;
    private string _trashRoot = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _originalDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        _sandboxRoot = Path.Combine(
            AppContext.BaseDirectory,
            "recycle-bin-test-" + Guid.NewGuid().ToString("N")[..8]);
        _trashRoot = Path.Combine(_sandboxRoot, "xdg-data");
        Directory.CreateDirectory(_sandboxRoot);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _trashRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _originalDataHome);
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
        Assert.IsFalse(RecycleBin.IsSupported(Path.Combine(_sandboxRoot, "missing")));
        Assert.IsFalse(RecycleBin.IsSupported(null));
    }

    [TestMethod]
    public void Move_Directory_ProducesRestorableTrashEntry()
    {
        var source = Path.Combine(_sandboxRoot, "instance-under-test");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "options.txt"), "fov:0.0");

        Assert.IsTrue(RecycleBin.IsSupported(source), "工作区位于用户主目录下，应支持回收站");

        var outcome = RecycleBin.Move(source, out var error);

        Assert.AreEqual(RecycleBin.Outcome.Moved, outcome, error);
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

        var outcome = RecycleBin.Move(source, out var error);

        Assert.AreEqual(RecycleBin.Outcome.Moved, outcome, error);
        Assert.IsFalse(File.Exists(source));
        Assert.IsTrue(File.Exists(Path.Combine(_trashRoot, "Trash", "files", "world.zip")));
    }

    [TestMethod]
    public void Move_RepeatedName_GetsSuffixInsteadOfOverwriting()
    {
        var first = Path.Combine(_sandboxRoot, "saves");
        Directory.CreateDirectory(first);
        Assert.AreEqual(RecycleBin.Outcome.Moved, RecycleBin.Move(first, out _));

        var second = Path.Combine(_sandboxRoot, "saves");
        Directory.CreateDirectory(second);
        Assert.AreEqual(RecycleBin.Outcome.Moved, RecycleBin.Move(second, out _));

        var files = Path.Combine(_trashRoot, "Trash", "files");
        Assert.IsTrue(Directory.Exists(Path.Combine(files, "saves")));
        Assert.IsTrue(Directory.Exists(Path.Combine(files, "saves.2")), "重名条目应加后缀，不能覆盖已有回收站内容");
    }

    [TestMethod]
    public void Move_MissingPath_ReportsFailure()
    {
        var outcome = RecycleBin.Move(Path.Combine(_sandboxRoot, "not-there"), out var error);
        Assert.AreEqual(RecycleBin.Outcome.Failed, outcome);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }
}