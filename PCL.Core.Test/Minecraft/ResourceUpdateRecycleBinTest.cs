using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class ResourceUpdateRecycleBinTest
{
    private string _root = null!;
    private ResourceUpdateRecycleBin _bin = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "PCL-Recycle-Test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _bin = new ResourceUpdateRecycleBin(_root);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    public void CreatesResourceFolders()
    {
        _bin.InitializeFolders();
        foreach (var folder in new[] { "mods", "resourcepacks", "shaderpacks", "schematics", "datapacks" })
            Assert.IsTrue(Directory.Exists(Path.Combine(_root, "recycle", folder)));
    }

    [TestMethod]
    [DataRow("mods")]
    [DataRow("resourcepacks")]
    [DataRow("shaderpacks")]
    [DataRow("saves/world/datapacks")]
    [DataRow("mods/nested")]
    [DataRow("labymod-neo/fabric/1.21/mods")]
    public void UpdateAndUndoPreserveDirectoryAndContent(string folder)
    {
        var old = _Write(folder + "/resource-1.jar", "old version");
        var updated = Path.Combine(_root, folder, "resource-2.jar");
        var downloaded = _Write("download/new.jar", "new version");
        Assert.IsFalse(_bin.CanUndo(old));

        _bin.Replace(old, updated, downloaded);
        var backup = Path.Combine(_root, "recycle", folder, "resource-1.jar");
        Assert.IsFalse(File.Exists(old));
        Assert.IsFalse(File.Exists(downloaded));
        Assert.AreEqual("old version", File.ReadAllText(backup));
        Assert.AreEqual("new version", File.ReadAllText(updated));
        // 从磁盘重新加载，模拟重启后的撤回。
        var reopened = new ResourceUpdateRecycleBin(_root);
        Assert.IsTrue(reopened.CanUndo(updated));
        reopened.Undo(updated);
        Assert.AreEqual("old version", File.ReadAllText(old));
        Assert.IsFalse(File.Exists(updated));
        Assert.IsFalse(File.Exists(backup));
        Assert.IsFalse(reopened.CanUndo(old));
        Assert.AreEqual(0, Directory.GetFiles(_root, ".pcl-undo-*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public void SameFileNameCanBeUpdatedAndUndone()
    {
        var resource = _Write("mods/example.jar", "v1");
        _bin.Replace(resource, resource, _Write("download/new.jar", "v2"));
        Assert.AreEqual("v2", File.ReadAllText(resource));
        Assert.IsTrue(_bin.CanUndo(resource));
        _bin.Undo(resource);
        Assert.AreEqual("v1", File.ReadAllText(resource));
        Assert.IsFalse(_bin.CanUndo(resource));
    }

    [TestMethod]
    public void PreservesDisabledOriginalAfterTogglingUpdatedResource()
    {
        var old = _Write("mods/example-1.jar.disabled", "v1");
        var updated = Path.Combine(_root, "mods/example-2.jar.disabled");
        _bin.Replace(old, updated, _Write("download/new.jar", "v2"));
        var enabled = updated[..^".disabled".Length];
        File.Move(updated, enabled);
        Assert.IsTrue(_bin.CanUndo(enabled));
        _bin.Undo(enabled);
        Assert.AreEqual("v1", File.ReadAllText(old));
        Assert.IsFalse(File.Exists(enabled));
        Assert.IsFalse(_bin.CanUndo(old));
    }

    [TestMethod]
    public void RetainsExistingBackupWithSameName()
    {
        _Write("recycle/mods/example.jar", "older backup");
        var old = _Write("mods/example.jar", "v1");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "v2"));
        Assert.AreEqual(2, Directory.GetFiles(Path.Combine(_root, "recycle/mods")).Length);
        _bin.Undo(updated);
        Assert.AreEqual("v1", File.ReadAllText(old));
        Assert.AreEqual("older backup", File.ReadAllText(Path.Combine(_root, "recycle/mods/example.jar")));
    }

    [TestMethod]
    public void OnlyLastUpdateCanBeUndone()
    {
        var old = _Write("mods/example-1.jar", "v1");
        var second = Path.Combine(_root, "mods/example-2.jar");
        var third = Path.Combine(_root, "mods/example-3.jar");
        _bin.Replace(old, second, _Write("download/second.jar", "v2"));
        _bin.Replace(second, third, _Write("download/third.jar", "v3"));
        _bin.Undo(third);
        Assert.AreEqual("v2", File.ReadAllText(second));
        Assert.IsFalse(_bin.CanUndo(second));
        Assert.AreEqual("v1", File.ReadAllText(Path.Combine(_root, "recycle/mods/example-1.jar")));
    }

    [TestMethod]
    public void UnrelatedDestinationIsNotOverwritten()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = _Write("mods/new.jar", "unrelated");
        var downloaded = _Write("download/new.jar", "new");
        Assert.ThrowsExactly<IOException>(() => _bin.Replace(old, updated, downloaded));
        Assert.AreEqual("old", File.ReadAllText(old));
        Assert.AreEqual("unrelated", File.ReadAllText(updated));
        Assert.AreEqual("new", File.ReadAllText(downloaded));
    }

    [TestMethod]
    public void FailedInstallationRestoresOldFile()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        var downloaded = _Write("download/new.jar", "new");
        using (File.Open(old, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _bin.Replace(old, updated, downloaded));
        Assert.AreEqual("old", File.ReadAllText(old));
        Assert.AreEqual("new", File.ReadAllText(downloaded));
        Assert.IsFalse(File.Exists(updated));
        Assert.IsFalse(_bin.CanUndo(updated));
    }

    [TestMethod]
    public void FailedUndoRestoresUpdatedFileAndKeepsBackup()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var backup = Path.Combine(_root, "recycle/mods/old.jar");
        using (File.Open(backup, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsExactly<IOException>(() => _bin.Undo(updated));
        Assert.AreEqual("new", File.ReadAllText(updated));
        Assert.AreEqual("old", File.ReadAllText(backup));
        Assert.IsFalse(File.Exists(old));
        Assert.IsTrue(_bin.CanUndo(updated));
        _bin.Undo(updated);
        Assert.AreEqual("old", File.ReadAllText(old));
    }

    [TestMethod]
    public void CannotUndoWhenOriginalNameHasBeenReused()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        File.WriteAllText(old, "unrelated");
        Assert.ThrowsExactly<IOException>(() => _bin.Undo(updated));
        Assert.AreEqual("unrelated", File.ReadAllText(old));
        Assert.AreEqual("new", File.ReadAllText(updated));
        Assert.IsTrue(_bin.CanUndo(updated));
    }

    [TestMethod]
    public void MissingBackupOrModifiedResourceHidesUndo()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        File.WriteAllText(updated, "manually replaced");
        Assert.IsFalse(_bin.CanUndo(updated));
        Assert.ThrowsExactly<IOException>(() => _bin.Undo(updated));
        File.WriteAllText(updated, "new");
        Assert.IsTrue(_bin.CanUndo(updated));
        File.Delete(Path.Combine(_root, "recycle/mods/old.jar"));
        Assert.IsFalse(_bin.CanUndo(updated));
    }

    [TestMethod]
    public void UnsafeRecordCannotRestoreOutsideGameDirectory()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var recordPath = Directory.GetFiles(Path.Combine(_root, "recycle/.records"), "*.json").Single();
        var record = JsonNode.Parse(File.ReadAllText(recordPath))!;
        record["OriginalPath"] = "../outside.jar";
        File.WriteAllText(recordPath, record.ToJsonString());
        Assert.IsFalse(_bin.CanUndo(updated));
        Assert.ThrowsExactly<IOException>(() => _bin.Undo(updated));
        Assert.AreEqual("new", File.ReadAllText(updated));
    }

    [TestMethod]
    public void MissingDownloadDoesNotRemoveOldFile()
    {
        var old = _Write("mods/old.jar", "old");
        Assert.ThrowsExactly<FileNotFoundException>(() => _bin.Replace(old,
            Path.Combine(_root, "mods/new.jar"), Path.Combine(_root, "download/missing.jar")));
        Assert.AreEqual("old", File.ReadAllText(old));
    }

    [TestMethod]
    public void BulkUndoRestoresOnlyEligibleResourcesAndDeduplicatesSelection()
    {
        var oldMod = _Write("mods/mod-1.jar", "old mod");
        var newMod = Path.Combine(_root, "mods/mod-2.jar");
        _bin.Replace(oldMod, newMod, _Write("download/mod.jar", "new mod"));
        var oldPack = _Write("resourcepacks/pack-1.zip", "old pack");
        var newPack = Path.Combine(_root, "resourcepacks/pack-2.zip");
        _bin.Replace(oldPack, newPack, _Write("download/pack.zip", "new pack"));
        var untouched = _Write("mods/untouched.jar", "unchanged");

        var result = _bin.UndoMany(new[] { newMod, untouched, newPack, newMod });
        Assert.AreEqual(2, result.RestoredPaths.Count);
        Assert.AreEqual(1, result.SkippedPaths.Count);
        Assert.AreEqual(0, result.Failures.Count);
        Assert.AreEqual("old mod", File.ReadAllText(oldMod));
        Assert.AreEqual("old pack", File.ReadAllText(oldPack));
        Assert.AreEqual("unchanged", File.ReadAllText(untouched));
        Assert.IsFalse(File.Exists(newMod));
        Assert.IsFalse(File.Exists(newPack));
        Assert.IsFalse(_bin.CanUndo(oldMod));
        Assert.IsFalse(_bin.CanUndo(oldPack));
    }

    [TestMethod]
    public void BulkUndoContinuesAfterFailureAndPreservesFailedResource()
    {
        var blockedOld = _Write("shaderpacks/blocked-1.zip", "old shader");
        var blockedNew = Path.Combine(_root, "shaderpacks/blocked-2.zip");
        _bin.Replace(blockedOld, blockedNew, _Write("download/shader.zip", "new shader"));
        File.WriteAllText(blockedOld, "another resource");
        var old = _Write("mods/old.jar.disabled", "old mod");
        var updated = Path.Combine(_root, "mods/new.jar.disabled");
        _bin.Replace(old, updated, _Write("download/mod.jar", "new mod"));
        var missingOld = _Write("mods/missing-1.jar", "old missing");
        var missingNew = Path.Combine(_root, "mods/missing-2.jar");
        _bin.Replace(missingOld, missingNew, _Write("download/missing.jar", "new missing"));
        File.Delete(Path.Combine(_root, "recycle/mods/missing-1.jar"));

        var result = _bin.UndoMany(new[] { blockedNew, missingNew, updated });
        Assert.AreEqual(1, result.RestoredPaths.Count);
        Assert.AreEqual(1, result.SkippedPaths.Count);
        Assert.AreEqual(1, result.Failures.Count);
        Assert.IsTrue(result.Failures.ContainsKey(blockedNew));
        Assert.AreEqual("new shader", File.ReadAllText(blockedNew));
        Assert.AreEqual("another resource", File.ReadAllText(blockedOld));
        Assert.AreEqual("old shader", File.ReadAllText(Path.Combine(_root, "recycle/shaderpacks/blocked-1.zip")));
        Assert.AreEqual("new missing", File.ReadAllText(missingNew));
        Assert.AreEqual("old mod", File.ReadAllText(old));
        Assert.IsTrue(_bin.CanUndo(blockedNew));
    }

    private string _Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void EnabledAndDisabledCopiesHaveIndependentRecords()
    {
        var enabled = _Write("mods/example.jar", "enabled old");
        var disabled = _Write("mods/example.jar.disabled", "disabled old");
        _bin.Replace(enabled, enabled, _Write("download/enabled.jar", "same new"));
        _bin.Replace(disabled, disabled, _Write("download/disabled.jar", "same new"));
        Assert.AreEqual(2, Directory.GetFiles(Path.Combine(_root, "recycle/.records"), "*.json").Length);
        _bin.Undo(enabled);
        Assert.AreEqual("enabled old", File.ReadAllText(enabled));
        Assert.AreEqual("same new", File.ReadAllText(disabled));
        Assert.IsTrue(_bin.CanUndo(disabled));
        _bin.Undo(disabled);
        Assert.AreEqual("disabled old", File.ReadAllText(disabled));
    }

    [TestMethod]
    public void RootDirectoryContainmentUsesSingleSeparator()
    {
        var root = Path.GetPathRoot(_root)!;
        var path = Path.Combine(root, "mods", "example.jar");
        var method = typeof(ResourceUpdateRecycleBin).GetMethod("_GetContainedPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.AreEqual(path, method.Invoke(null, new object[] { root, path }));
    }

    [TestMethod]
    [DataRow("mods")]
    [DataRow("recycle")]
    [DataRow("recycle/mods")]
    [DataRow("recycle/.records")]
    [DataRow("recycle/.transactions")]
    [DataRow("download")]
    public void RejectsLinkedResourceBackupRecordAndDownloadDirectories(string relativeDirectory)
    {
        var external = Path.Combine(Path.GetTempPath(), "PCL-Recycle-External-" + Guid.NewGuid().ToString("N"));
        var linked = Path.Combine(_root, relativeDirectory);
        Directory.CreateDirectory(external);
        var old = _Write("mods/example.jar", "old");
        var download = _Write("download/example.jar", "new");
        if (Directory.Exists(linked)) Directory.Move(linked, Path.Combine(external, "moved"));
        var target = Directory.Exists(Path.Combine(external, "moved")) ? Path.Combine(external, "moved") : external;
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        _CreateJunction(linked, target);
        try
        {
            Assert.Throws<IOException>(() => _bin.Replace(old, old, download));
            Assert.AreEqual("old", File.ReadAllText(old));
            Assert.AreEqual("new", File.ReadAllText(download));
            Assert.IsFalse(Directory.EnumerateFiles(external, "*.json", SearchOption.AllDirectories).Any());
        }
        finally
        {
            Directory.Delete(linked);
            Directory.Delete(external, true);
        }
    }

    private static void _CreateJunction(string link, string target)
    {
        link = Path.GetFullPath(link);
        target = Path.GetFullPath(target);
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, output);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    public void RecoversEachInterruptedReplaceStage(int stage, bool sameName)
    {
        var old = _Write("mods/old.jar", "old");
        var updated = sameName ? old : Path.Combine(_root, "mods/new.jar");
        var download = _Write("download/new.jar", "new");
        var backup = Path.Combine(_root, "recycle/mods/old.jar");
        var record = new JsonObject
        {
            ["OriginalPath"] = Path.GetRelativePath(_root, old),
            ["UpdatedPath"] = Path.GetRelativePath(_root, updated),
            ["BackupPath"] = "mods/old.jar",
            ["OriginalHash"] = _Hash(old), ["UpdatedHash"] = _Hash(download)
        };
        _WriteJournal(new JsonObject { ["Operation"] = "Replace", ["Record"] = record.DeepClone() });
        if (stage >= 1)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Move(old, backup);
        }
        if (stage >= 2) File.Move(download, updated);
        if (stage >= 3) _WriteRecord(updated, record);
        var reopened = new ResourceUpdateRecycleBin(_root);
        reopened.RecoverPendingOperations();
        reopened.RecoverPendingOperations();
        if (stage >= 2)
        {
            Assert.AreEqual("new", File.ReadAllText(updated));
            Assert.IsTrue(reopened.CanUndo(updated));
            reopened.Undo(updated);
        }
        Assert.AreEqual("old", File.ReadAllText(old));
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_root, "recycle/.transactions"), "*.json").Length);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    [DataRow(4, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, true)]
    public void RecoversEachInterruptedUndoStage(int stage, bool sameName)
    {
        var old = _Write("mods/old.jar", "old");
        var updated = sameName ? old : Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var recordPath = Directory.GetFiles(Path.Combine(_root, "recycle/.records"), "*.json").Single();
        var record = JsonNode.Parse(File.ReadAllText(recordPath))!;
        var backup = Path.Combine(_root, "recycle/mods/old.jar");
        var staged = Path.Combine(_root, "mods/.pcl-undo-" + Guid.NewGuid().ToString("N") + ".tmp");
        _WriteJournal(new JsonObject
        {
            ["Operation"] = "Undo", ["Record"] = record.DeepClone(),
            ["ResourcePath"] = Path.GetRelativePath(_root, updated),
            ["StagedPath"] = Path.GetRelativePath(_root, staged), ["UndoRecordFile"] = Path.GetFileName(recordPath)
        });
        if (stage >= 1) File.Move(updated, staged);
        if (stage >= 2) File.Move(backup, old);
        if (stage >= 3) File.Delete(staged);
        if (stage >= 4) File.Delete(recordPath);
        var reopened = new ResourceUpdateRecycleBin(_root);
        reopened.RecoverPendingOperations();
        reopened.RecoverPendingOperations();
        if (stage == 0)
        {
            Assert.AreEqual("new", File.ReadAllText(updated));
            Assert.IsTrue(reopened.CanUndo(updated));
            reopened.Undo(updated);
        }
        Assert.AreEqual("old", File.ReadAllText(old));
        Assert.IsFalse(File.Exists(staged));
        Assert.IsFalse(reopened.CanUndo(old));
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_root, "recycle/.transactions"), "*.json").Length);
    }

    [TestMethod]
    public void BatchUpdateContinuesAfterConflict()
    {
        var blocked = _Write("mods/blocked-old.jar", "blocked old");
        var conflict = _Write("mods/blocked-new.jar", "unrelated");
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        var result = _bin.ReplaceMany(new[]
        {
            (blocked, _Write("download/blocked.jar", "blocked new"), conflict),
            (old, _Write("download/new.jar", "new"), updated)
        });
        Assert.AreEqual(1, result.UpdatedPaths.Count);
        Assert.AreEqual(1, result.Failures.Count);
        Assert.IsTrue(result.Failures.ContainsKey(blocked));
        Assert.AreEqual("blocked old", File.ReadAllText(blocked));
        Assert.AreEqual("unrelated", File.ReadAllText(conflict));
        Assert.AreEqual("new", File.ReadAllText(updated));
        Assert.IsTrue(_bin.CanUndo(updated));
    }

    [TestMethod]
    public void LegacyDisabledRecordCanBeReadAfterToggle()
    {
        var old = _Write("mods/old.jar.disabled", "old");
        var updated = Path.Combine(_root, "mods/new.jar.disabled");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var record = Directory.GetFiles(Path.Combine(_root, "recycle/.records"), "*.json").Single();
        var enabled = updated[..^".disabled".Length];
        var legacyPath = _GetRecordPath(enabled);
        File.Move(record, legacyPath);
        File.Move(updated, enabled);
        Assert.IsTrue(_bin.CanUndo(enabled));
        _bin.Undo(enabled);
        Assert.AreEqual("old", File.ReadAllText(old));
        Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_root, "recycle/.records"), "*.json").Length);
    }

    [TestMethod]
    public void AmbiguousToggleDoesNotRestoreSibling()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        File.Move(updated, updated + ".disabled");
        File.WriteAllText(updated + ".old", "new");
        Assert.IsFalse(_bin.CanUndo(updated + ".disabled"));
        Assert.IsFalse(_bin.CanUndo(updated + ".old"));
    }

    [TestMethod]
    public void RecoveryPreservesConflictingFileAndJournal()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var record = JsonNode.Parse(File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "recycle/.records"), "*.json").Single()))!;
        var staged = Path.Combine(_root, "mods/.pcl-undo-" + Guid.NewGuid().ToString("N") + ".tmp");
        var journal = _WriteJournal(new JsonObject
        {
            ["Operation"] = "Undo", ["Record"] = record.DeepClone(), ["ResourcePath"] = "mods/new.jar",
            ["StagedPath"] = Path.GetRelativePath(_root, staged),
            ["UndoRecordFile"] = Path.GetFileName(_GetRecordPath(updated))
        });
        File.Move(updated, staged);
        File.WriteAllText(old, "unrelated");
        Assert.Throws<IOException>(() => new ResourceUpdateRecycleBin(_root).RecoverPendingOperations());
        Assert.AreEqual("unrelated", File.ReadAllText(old));
        Assert.AreEqual("new", File.ReadAllText(staged));
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "recycle/mods/old.jar")));
        Assert.IsTrue(File.Exists(journal));
    }

    private string _WriteJournal(JsonObject journal) =>
        _Write("recycle/.transactions/" + Guid.NewGuid().ToString("N") + ".json", journal.ToJsonString());

    private string _GetRecordPath(string updated) => (string)typeof(ResourceUpdateRecycleBin)
        .GetMethod("_GetRecordPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
        .Invoke(_bin, new object[] { updated })!;

    private void _WriteRecord(string updated, JsonObject record)
    {
        var path = _GetRecordPath(updated);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, record.ToJsonString());
    }

    private static string _Hash(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    [TestMethod]
    [DataRow("mods")]
    [DataRow("recycle/mods")]
    [DataRow("recycle/.records")]
    public void UndoRejectsDirectoriesLinkedAfterUpdate(string relativeDirectory)
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var linked = Path.GetFullPath(Path.Combine(_root, relativeDirectory));
        var moved = Path.Combine(_root, "redirected");
        Directory.Move(linked, moved);
        _CreateJunction(linked, moved);
        try
        {
            Assert.IsFalse(_bin.CanUndo(updated));
            Assert.Throws<IOException>(() => _bin.Undo(updated));
            Assert.AreEqual("new", File.ReadAllText(updated));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "recycle/mods/old.jar")));
        }
        finally { Directory.Delete(linked); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RejectsLinkedGameRootAndAncestor(bool ancestor)
    {
        var storage = Path.Combine(_root, "storage");
        var linked = Path.Combine(_root, "linked");
        var suffix = ancestor ? "game" : "";
        var old = _Write("storage/" + suffix + "/mods/old.jar", "old");
        var downloaded = _Write("download/new.jar", "new");
        Directory.CreateDirectory(storage);
        _CreateJunction(linked, storage);
        try
        {
            var game = Path.Combine(linked, suffix);
            var bin = new ResourceUpdateRecycleBin(game);
            Assert.Throws<IOException>(() => bin.InitializeFolders());
            Assert.Throws<IOException>(() => bin.Replace(Path.Combine(game, "mods/old.jar"),
                Path.Combine(game, "mods/new.jar"), downloaded));
            Assert.AreEqual("old", File.ReadAllText(old));
            Assert.IsFalse(Directory.Exists(Path.Combine(storage, suffix, "recycle")));
        }
        finally { Directory.Delete(linked); }
    }

    [TestMethod]
    public void NormalExternalDownloadCacheIsSupported()
    {
        var external = Path.Combine(Path.GetTempPath(), "PCL-Download-" + Guid.NewGuid().ToString("N") + ".jar");
        File.WriteAllText(external, "new");
        try
        {
            var old = _Write("mods/old.jar", "old");
            var updated = Path.Combine(_root, "mods/new.jar");
            _bin.Replace(old, updated, external);
            Assert.IsFalse(File.Exists(external));
            _bin.Undo(updated);
            Assert.AreEqual("old", File.ReadAllText(old));
            Assert.AreEqual(0, Directory.EnumerateFiles(Path.Combine(_root, "mods"), ".pcl-*").Count());
        }
        finally { File.Delete(external); }
    }

    [TestMethod]
    public void InterruptedReplaceRemovesVerifiedLocalStagingFile()
    {
        var old = _Write("mods/old.jar", "old");
        var staged = _Write("mods/.pcl-update-" + Guid.NewGuid().ToString("N") + ".tmp", "new");
        var record = new JsonObject
        {
            ["OriginalPath"] = "mods/old.jar", ["UpdatedPath"] = "mods/new.jar", ["BackupPath"] = "mods/old.jar",
            ["OriginalHash"] = _Hash(old), ["UpdatedHash"] = _Hash(staged)
        };
        _WriteJournal(new JsonObject
        {
            ["Operation"] = "Replace", ["Record"] = record,
            ["StagedPath"] = Path.GetRelativePath(_root, staged)
        });
        Directory.CreateDirectory(Path.Combine(_root, "recycle/mods"));
        File.Move(old, Path.Combine(_root, "recycle/mods/old.jar"));
        _bin.RecoverPendingOperations();
        Assert.AreEqual("old", File.ReadAllText(old));
        Assert.IsFalse(File.Exists(staged));
    }

    [TestMethod]
    public void RecoveryRejectsRedirectedTransactionDirectory()
    {
        var target = Path.Combine(_root, "external");
        Directory.CreateDirectory(target);
        var sentinel = _Write("external/sentinel.json", "unchanged");
        var linked = Path.Combine(_root, "recycle/.transactions");
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        _CreateJunction(linked, target);
        try
        {
            Assert.Throws<IOException>(() => _bin.RecoverPendingOperations());
            Assert.AreEqual("unchanged", File.ReadAllText(sentinel));
        }
        finally { Directory.Delete(linked); }
    }

    [TestMethod]
    public void CorruptUnrelatedRecordDoesNotHideValidUndo()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        _Write("recycle/.records/broken.json", "{");
        Assert.IsTrue(_bin.CanUndo(updated));
        File.Move(updated, updated + ".disabled");
        Assert.IsTrue(_bin.CanUndo(updated + ".disabled"));
    }

    [TestMethod]
    public void InvalidPreviousRecordNameIsRejectedBeforeRecoveryWrites()
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var recordPath = _GetRecordPath(updated);
        var record = JsonNode.Parse(File.ReadAllText(recordPath))!;
        File.Delete(recordPath);
        var journal = _WriteJournal(new JsonObject
        {
            ["Operation"] = "Replace", ["Record"] = record,
            ["PreviousRecordFile"] = "../unrelated.json"
        });
        Assert.Throws<IOException>(() => _bin.RecoverPendingOperations());
        Assert.IsFalse(File.Exists(recordPath));
        Assert.AreEqual("new", File.ReadAllText(updated));
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(_root, "recycle/mods/old.jar")));
        Assert.IsTrue(File.Exists(journal));
    }

    [TestMethod]
    [DataRow("Replace")]
    [DataRow("Undo")]
    public void RecoveryDoesNotDeleteUnrelatedUndoRecord(string operation)
    {
        var old = _Write("mods/old.jar", "old");
        var updated = Path.Combine(_root, "mods/new.jar");
        _bin.Replace(old, updated, _Write("download/new.jar", "new"));
        var unrelatedOld = _Write("mods/other-old.jar", "old");
        var unrelatedUpdated = Path.Combine(_root, "mods/other-new.jar");
        _bin.Replace(unrelatedOld, unrelatedUpdated, _Write("download/other.jar", "new"));
        var record = JsonNode.Parse(File.ReadAllText(_GetRecordPath(updated)))!;
        var unrelatedRecordPath = _GetRecordPath(unrelatedUpdated);
        var unrelatedRecord = File.ReadAllText(unrelatedRecordPath);
        var staged = Path.Combine(_root, "mods/.pcl-undo-" + Guid.NewGuid().ToString("N") + ".tmp");
        var journal = _WriteJournal(new JsonObject
        {
            ["Operation"] = operation, ["Record"] = record,
            ["ResourcePath"] = "mods/new.jar",
            ["StagedPath"] = operation == "Undo" ? Path.GetRelativePath(_root, staged) : "",
            ["PreviousRecordFile"] = Path.GetFileName(unrelatedRecordPath),
            ["UndoRecordFile"] = Path.GetFileName(unrelatedRecordPath)
        });
        if (operation == "Undo")
        {
            File.Move(updated, staged);
            File.Move(Path.Combine(_root, "recycle/mods/old.jar"), old);
        }
        Assert.Throws<IOException>(() => _bin.RecoverPendingOperations());
        Assert.AreEqual(unrelatedRecord, File.ReadAllText(unrelatedRecordPath));
        Assert.IsTrue(_bin.CanUndo(unrelatedUpdated));
        Assert.IsTrue(File.Exists(journal));
        if (operation == "Undo") Assert.AreEqual("new", File.ReadAllText(staged));
    }
}
