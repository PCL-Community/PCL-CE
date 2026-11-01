using System.Diagnostics;
using System.Globalization;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Network;

namespace PCL;

public static partial class DownloadUi
{
    private static void McDownloadOptiFineInstall(
        string baseMcFolderHome,
        string target,
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        bool useJavaWrapper,
        Version javaVersion
    )
    {
        var java = _SelectOptiFineJava(task, javaVersion);
        if (java is null)
            return;

        var arguments = _BuildOptiFineInstallArguments(
            baseMcFolderHome,
            target,
            useJavaWrapper,
            java);
        lock (InstallSyncLock)
        {
            var info = _CreateOptiFineInstallProcessInfo(baseMcFolderHome, java, arguments);
            Common.Log($"[Download] 开始安装 OptiFine：{target}");
            var totalLength = 0;
            var process = new Process
            {
                StartInfo = info
            };
            var lastResult = "";

            void HandleOutputLine(string line)
            {
                lastResult = line;
                if (Common.modeDebug)
                    Common.Log($"[Installer] {lastResult}");
                totalLength += 1;
                task.Progress += 0.9d / 7000d;
            }

            using (var outputWaitHandle = new AutoResetEvent(false))
            {
                using (var errorWaitHandle = new AutoResetEvent(false))
                {
                    process.OutputDataReceived += _CreateOptiFineOutputHandler(
                        process,
                        task,
                        outputWaitHandle,
                        "读取 OptiFine 安装器信息失败",
                        HandleOutputLine
                    );
                    process.ErrorDataReceived += _CreateOptiFineOutputHandler(
                        process,
                        task,
                        errorWaitHandle,
                        "读取 OptiFine 安装器错误信息失败",
                        HandleOutputLine
                    );
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    while (!process.HasExited)
                        Thread.Sleep(10);
                    outputWaitHandle.WaitOne(10000);
                    errorWaitHandle.WaitOne(10000);
                    process.Dispose();
                    if (totalLength < 1000 || lastResult.Contains("at "))
                        throw new Exception(
                            Lang.Text(
                                "Minecraft.Download.Error.InstallerFailedLastLine",
                                lastResult));
                }
            }
        }
    }

    private static JavaEntry? _SelectOptiFineJava(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        Version javaVersion)
    {
        JavaEntry java;
        lock (JavaRuntime.JavaLock)
        {
            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                javaVersion,
                enforceVersionRange: true
            );
            if (java is not null)
                return java;

            if (!JavaRuntime.JavaDownloadConfirm(
                    Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));

            var downloadJavaMajor = javaVersion.Major >= 9 ? javaVersion.Major : 8;
            var javaLoader = JavaRuntime.GetJavaDownloadLoader();
            try
            {
                javaLoader.Start(downloadJavaMajor, true);
                while (javaLoader.State == LoadState.Loading && !task.IsAborted)
                    Thread.Sleep(10);
            }
            finally
            {
                javaLoader.Abort();
            }

            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                javaVersion,
                enforceVersionRange: true
            );
            if (task.IsAborted)
                return null;
            if (java is null)
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
        }

        return java;
    }

    private static string _BuildOptiFineInstallArguments(
        string baseMcFolderHome,
        string target,
        bool useJavaWrapper,
        JavaEntry java)
    {
        string arguments;
        if (useJavaWrapper && !(dynamic)Config.Launch.DisableJlw)
            arguments =
                $"-Doolloo.jlw.tmpdir=\"{Common.pathPure.TrimEnd('\\')}\" -Duser.home=\"{baseMcFolderHome.TrimEnd('\\')}\" -cp \"{target}\" -jar \"{GameLaunch.ExtractJavaWrapper()}\" optifine.Installer";
        else
            arguments =
                $"-Duser.home=\"{baseMcFolderHome.TrimEnd('\\')}\" -cp \"{target}\" optifine.Installer";

        if (java.Installation.MajorVersion >= 9)
            arguments =
                $"--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED {arguments}";

        return arguments;
    }

    private static ProcessStartInfo _CreateOptiFineInstallProcessInfo(
        string baseMcFolderHome,
        JavaEntry java,
        string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = java.Installation.JavaExePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = Common.ShortenPath(baseMcFolderHome)
        };
        if (info.EnvironmentVariables.ContainsKey("appdata"))
            info.EnvironmentVariables["appdata"] = baseMcFolderHome;
        else
            info.EnvironmentVariables.Add("appdata", baseMcFolderHome);

        return info;
    }

    private static DataReceivedEventHandler _CreateOptiFineOutputHandler(
        Process process,
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        AutoResetEvent waitHandle,
        string readErrorMessage,
        Action<string> handleLine
    )
    {
        return (_, e) =>
        {
            try
            {
                if (e.Data is null)
                    waitHandle.Set();
                else
                    handleLine(e.Data);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Common.Log(ex, readErrorMessage);
            }

            try
            {
                if (task.State == LoadState.Aborted && !process.HasExited)
                {
                    Common.Log("[Installer] 由于任务取消，已中止 OptiFine 安装");
                    process.Kill();
                }
            }
            catch
            {
            }
        };
    }

    private static void _InstallNewOptiFine(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        GameDownload.DlOptiFineListEntry downloadInfo,
        string mcFolder,
        string target,
        Version javaVersion
    )
    {
        var baseMcFolderHome = Launcher.RequestTaskTempFolder();
        var baseMcFolder = Path.Combine(baseMcFolderHome, ".minecraft");
        try
        {
            var inherit = downloadInfo.Inherit
                ?? throw new InvalidOperationException("OptiFine 版本缺少对应的 Minecraft 版本");
            var inheritedVersionFolder = Path.Combine(
                baseMcFolder,
                "versions",
                inherit);
            if (Directory.Exists(inheritedVersionFolder))
                Common.DeleteDirectory(inheritedVersionFolder);
            Directory.CreateDirectory(inheritedVersionFolder);
            GameFolders.McFolderLauncherProfilesJsonCreate(baseMcFolder);
            Common.CopyFile(
                Path.Combine(
                    mcFolder,
                    "versions",
                    inherit,
                    $"{inherit}.json"),
                Path.Combine(inheritedVersionFolder, $"{inherit}.json")
            );
            Common.CopyFile(
                Path.Combine(
                    mcFolder,
                    "versions",
                    inherit,
                    $"{inherit}.jar"),
                Path.Combine(inheritedVersionFolder, $"{inherit}.jar")
            );
            task.Progress = 0.06d;
            var useJavaWrapper = Common.IsUtf8CodePage();

            _RunOptiFineInstallerWithRetry(
                task,
                baseMcFolderHome,
                target,
                useJavaWrapper,
                javaVersion);

            task.Progress = 0.96d;
            File.Delete(Path.Combine(baseMcFolder, "launcher_profiles.json"));
            Common.CopyDirectory(baseMcFolder, mcFolder);
            task.Progress = 0.98d;
            File.Delete(target);
            Common.DeleteDirectory(baseMcFolderHome);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.OptiFineInstallFailed.MethodA"),
                ex);
        }
    }

    private static void _RunOptiFineInstallerWithRetry(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        string baseMcFolderHome,
        string target,
        bool useJavaWrapper,
        Version javaVersion
    )
    {
        var useWrapper = useJavaWrapper;
        while (true)
            try
            {
                McDownloadOptiFineInstall(baseMcFolderHome, target, task, useWrapper, javaVersion);
                return;
            }
            catch (Exception ex)
            {
                if (useWrapper)
                    throw new Exception(
                        Lang.Text("Minecraft.Download.Error.OptiFineInstallerRunFailed"),
                        ex);
                Common.Log(ex, "不使用 JavaWrapper 安装 OptiFine 失败，将使用 JavaWrapper 并重试");
                useWrapper = true;
            }
    }

    private static void _InstallLegacyOptiFine(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        GameDownload.DlOptiFineListEntry downloadInfo,
        string mcFolder,
        string versionFolder,
        string id
    )
    {
        try
        {
            var inherit = downloadInfo.Inherit
                ?? throw new InvalidOperationException("OptiFine 版本缺少对应的 Minecraft 版本");
            var nameFile = downloadInfo.NameFile
                ?? throw new InvalidOperationException("OptiFine 版本缺少文件名");
            Directory.CreateDirectory(versionFolder);
            task.Progress = 0.1d;
            var versionJarPath = Path.Combine(versionFolder, $"{id}.jar");
            if (File.Exists(versionJarPath))
                File.Delete(versionJarPath);
            Common.CopyFile(
                Path.Combine(
                    mcFolder,
                    "versions",
                    inherit,
                    $"{inherit}.jar"),
                versionJarPath);
            task.Progress = 0.7d;
            var inheritInstance =
                new McInstance(Path.Combine(mcFolder, "versions", inherit));
            var releaseDate = (downloadInfo.ReleaseTime ?? inheritInstance.releaseTime)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var optiFineVersion = nameFile
                .Replace("OptiFine_", "")
                .Replace(".jar", "")
                .Replace("preview_", "");
            var jsonObject = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["inheritsFrom"] = downloadInfo.Inherit,
                ["time"] = $"{releaseDate}T23:33:33+08:00",
                ["releaseTime"] = $"{releaseDate}T23:33:33+08:00",
                ["type"] = "release",
                ["libraries"] = new object[]
                {
                    new { name = $"optifine:OptiFine:{optiFineVersion}" },
                    new { name = "net.minecraft:launchwrapper:1.12" }
                },
                ["mainClass"] = "net.minecraft.launchwrapper.Launch"
            };
            task.Progress = 0.8d;
            if (inheritInstance.IsOldJson)
            {
                jsonObject["minimumLauncherVersion"] = 18;
                jsonObject["minecraftArguments"] =
                    $"{inheritInstance.JsonObject["minecraftArguments"]}  --tweakClass optifine.OptiFineTweaker";
            }
            else
            {
                jsonObject["minimumLauncherVersion"] = "21";
                jsonObject["arguments"] = new
                {
                    game = (string[])
                    [
                        "--tweakClass",
                        "optifine.OptiFineTweaker"
                    ]
                };
            }

            var json = JsonSerializer.Serialize(
                jsonObject,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            Common.WriteFile(Path.Combine(versionFolder, $"{id}.json"), json);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.OptiFineInstallFailed.MethodB"),
                ex);
        }
    }
}