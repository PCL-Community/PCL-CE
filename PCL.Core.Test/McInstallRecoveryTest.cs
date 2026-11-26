using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;
using static PCL.Loading;

namespace PCL.Core.Test;

[TestClass]
public class McInstallRecoveryTest
{
    private readonly string _root = Directory.CreateTempSubdirectory().FullName;
    private McInstallRequest Request(string name = "Pack") => new() { targetInstanceName = name, targetInstanceFolder = Path.Combine(_root, name) };
    private static string Write(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private sealed class Install : LoaderBase
    {
        public override void Start(object? input = null, bool isForceRestart = false) => State = LoadState.Loading;
        public override void Abort() => State = LoadState.Aborted;
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    public async Task FailedInstallReleasesNameWithoutTouchingDownloadOrNewOwner()
    {
        var request = Request();
        var folder = request.targetInstanceFolder;
        var archive = Write(Path.Combine(_root, "download.mrpack"), "archive");
        var first = new Install();
        var retry = new Install();
        using (InstallOutputs.AcquireLock(folder))
            Assert.ThrowsExactly<IOException>(() => McInstallRecovery.Start(first, request, folder));
        Assert.IsFalse(Directory.Exists(folder));
        McInstallRecovery.Start(first, request, folder, modpackFile: archive);
        Assert.ThrowsExactly<Exception>(() => McInstallRecovery.Start(retry, request, folder));
        first.Abort();
        await McInstallRecovery.HandleTerminalStateAsync(first, folder);
        Assert.IsFalse(Directory.Exists(folder));
        Assert.AreEqual("archive", File.ReadAllText(archive));

        // 模拟下一次启动在发布标记前退出；名称校验必须检查用户输入的那个目录。
        using (var interrupted = new InstallOutputs(folder)) interrupted.Begin(hadJson: false, createdFolder: true);
        var marker = Path.Combine(folder, ".pcl-install");
        var pending = marker + "." + File.ReadAllText(marker);
        File.Move(marker, pending);
        File.WriteAllText(pending, "");
        File.Delete(Path.Combine(folder, ".pclignore"));
        var validator = new FolderNameValidator(_root) { AllowExistingFolder = McInstallRecovery.CanRetry };
        Assert.IsTrue(validator.Validate("Pack").IsValid);
        var alias = Request("pack");
        var sameFolder = Directory.Exists(alias.targetInstanceFolder);
        Assert.AreEqual(sameFolder, validator.Validate("pack").IsValid);
        var retryRequest = sameFolder ? alias : request;
        McInstallRecovery.Start(retry, retryRequest, retryRequest.targetInstanceFolder, requireNewFolder: true);
        Assert.IsFalse(File.Exists(pending));
        await McInstallRecovery.HandleTerminalStateAsync(first, retryRequest.targetInstanceFolder);
        Assert.IsTrue(File.Exists(Path.Combine(folder, ".pclignore")));
        File.WriteAllText(Path.Combine(folder, "override.txt"), "partial");
        retry.Abort();
        await McInstallRecovery.HandleTerminalStateAsync(retry, retryRequest.targetInstanceFolder);
        Assert.IsFalse(Directory.Exists(folder));
    }

    [TestMethod]
    public async Task InterruptedOverwriteRestoresCoreAndPreservesUserFilesOnRetry()
    {
        var request = Request();
        var folder = request.targetInstanceFolder;
        var prior = Write(Path.Combine(folder, McInstallRecovery.BackupFolderName, "Pack.json"), "prior");
        var json = Write(Path.Combine(folder, "Pack.json"), "installed");
        var original = Write(Path.Combine(folder, "原始整合包.zip"), "user archive");
        var extra = Path.Combine(folder, "config", "new.txt");
        var jar = Path.Combine(folder, "Pack.jar");
        // 模拟进程退出：关闭清单，不执行终态回滚。
        using (var interrupted = new InstallOutputs(folder))
        {
            interrupted.Begin(hadJson: true, createdFolder: false);
            interrupted.Register([json, jar], backUpExisting: true);
            File.Delete(json);
            File.WriteAllText(jar, "partial jar");
        }
        var install = new Install();
        Assert.IsTrue(McInstallRecovery.RequiresOverwrite(folder, "Pack"));
        McInstallRecovery.Start(install, request, folder, overwrite: true);
        Assert.AreEqual("installed", File.ReadAllText(json));
        Assert.IsFalse(File.Exists(jar));
        McInstallRecovery.TrackOutputs(install, [extra, original], backUpExisting: true);
        File.WriteAllText(original, "overwritten");
        Write(extra, "partial");
        File.WriteAllText(json, "changed");
        install.Abort();
        await McInstallRecovery.HandleTerminalStateAsync(install, folder);
        Assert.AreEqual("installed", File.ReadAllText(json));
        Assert.AreEqual("prior", File.ReadAllText(prior));
        Assert.AreEqual("user archive", File.ReadAllText(original));
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(extra)));
    }

    [TestMethod]
    public void CleanupFailuresPreserveCommittedInstallAndPriorBackup()
    {
        var folder = Request().targetInstanceFolder;
        var json = Write(Path.Combine(folder, "Pack.json"), "installed");
        var jar = Write(Path.Combine(folder, "Pack.jar"), "installed jar");
        var backup = Path.Combine(folder, McInstallRecovery.BackupFolderName);
        var prior = Write(Path.Combine(backup, "Pack.json"), "prior");
        Directory.CreateDirectory(Path.Combine(backup, "Pack.jar")); // 阻止第二个备份文件写入。
        using var outputs = new InstallOutputs(folder);
        outputs.Begin(hadJson: true, createdFolder: false);
        outputs.Register([json, jar], backUpExisting: true);
        File.WriteAllText(json, "updated");
        var ignore = Path.Combine(folder, ".pclignore");
        File.Delete(ignore);
        Directory.CreateDirectory(ignore); // 让 File.Delete 在三个平台上都失败。
        outputs.Finish(rollback: false, coreBackup: [json, jar]);
        Assert.AreEqual("updated", File.ReadAllText(json));
        Assert.AreEqual("prior", File.ReadAllText(prior));
        Assert.IsNull(outputs.ReadInterruptedOrigin());
    }
}
