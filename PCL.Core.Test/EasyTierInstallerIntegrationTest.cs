using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.Link.Scaffolding.EasyTier;

namespace PCL.Core.Test;

[TestClass]
[DoNotParallelize]
public sealed class EasyTierInstallerIntegrationTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CliRunAsyncReadsStandardOutput()
    {
        var previous = Paths.SharedLocalData;
        var root = Path.Combine(
            Path.GetTempPath(),
            $"pcl-easytier-cli-test-{Guid.NewGuid():N}");
        Paths.SharedLocalData = root;
        try
        {
            Directory.CreateDirectory(EasyTierMetadata.InstallRoot);
            await File.WriteAllTextAsync(
                EasyTierMetadata.CliPath,
                "#!/bin/sh\necho 'hello-easytier'\nexit 0\n",
                TestContext.CancellationToken);
            File.SetUnixFileMode(
                EasyTierMetadata.CliPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var result = await EasyTierCli.RunAsync(
                "--version",
                TimeSpan.FromSeconds(5),
                TestContext.CancellationToken);

            Assert.AreEqual(0, result.ExitCode);
            Assert.Contains("hello-easytier", result.StandardOutput);
        }
        finally
        {
            Paths.SharedLocalData = previous;
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 忽略测试清理失败
            }
        }
    }

    [TestMethod]
    public async Task InstallArchiveCreatesRequiredFilesAndExecutableBits()
    {
        var previous = Paths.SharedLocalData;
        var root = Path.Combine(
            Path.GetTempPath(),
            $"pcl-easytier-test-{Guid.NewGuid():N}");
        Paths.SharedLocalData = root;
        try
        {
            var package = EasyTierMetadata.Package;
            const string version = EasyTierMetadata.CurrentEasyTierVer;
            Directory.CreateDirectory(root);
            var archivePath = Path.Combine(root, package.GetArchiveName(version));

            await using (var archive = await ZipFile.OpenAsync(
                             archivePath,
                             ZipArchiveMode.Create,
                             TestContext.CancellationToken))

            {
                foreach (var fileName in package.RequiredFiles)
                {
                    var entry = archive.CreateEntry($"{package.FolderName}/{fileName}");
                    await using var stream = await entry.OpenAsync(TestContext.CancellationToken);
                    stream.WriteByte(0);
                }
            }

            var installed =
                await EasyTierInstaller.InstallArchiveAsync(
                    archivePath,
                    version,
                    package,
                    TestContext.CancellationToken);

            Assert.IsTrue(installed);
            Assert.IsTrue(EasyTierMetadata.IsInstalled);
            Assert.IsTrue(File.Exists(EasyTierMetadata.CorePath));
            Assert.IsTrue(File.Exists(EasyTierMetadata.CliPath));

            if (!OperatingSystem.IsWindows())
            {
                Assert.IsTrue(
                    File
                        .GetUnixFileMode(EasyTierMetadata.CorePath)
                        .HasFlag(UnixFileMode.UserExecute));
                Assert.IsTrue(
                    File
                        .GetUnixFileMode(EasyTierMetadata.CliPath)
                        .HasFlag(UnixFileMode.UserExecute));
            }
        }
        finally
        {
            Paths.SharedLocalData = previous;
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch
            {
                // 忽略测试清理失败
            }
        }
    }
}