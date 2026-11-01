using System.Diagnostics;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;

namespace PCL;

public static partial class DownloadUi
{
    private static void ForgelikeInjector(
        string target,
        Loading.LoaderTask<bool, bool> task,
        string mcFolder,
        bool useJavaWrapper,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType
    )
    {
        var java = _SelectForgelikeJava(task);
        if (java is null)
            return;

        var arguments = _BuildForgelikeInstallArguments(target, mcFolder, useJavaWrapper, java);
        lock (InstallSyncLock)
        {
            var info = new ProcessStartInfo
            {
                FileName = java.Installation.JavaExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            var loaderName = Common.GetStringFromEnum(forgeType) ?? forgeType.ToString();
            Common.Log($"[Download] 开始安装 {loaderName}：{arguments}");
            var process = new Process
            {
                StartInfo = info
            };
            var lastResults = new Queue<string>();
            using var outputWaitHandle = new AutoResetEvent(false);
            using var errorWaitHandle = new AutoResetEvent(false);
            process.OutputDataReceived += _CreateForgelikeOutputHandler(
                process,
                task,
                outputWaitHandle,
                loaderName,
                lastResults,
                $"读取 {loaderName} 安装器信息失败"
            );
            process.ErrorDataReceived += _CreateForgelikeOutputHandler(
                process,
                task,
                errorWaitHandle,
                loaderName,
                lastResults,
                $"读取 {loaderName} 安装器错误信息失败"
            );
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            while (!process.HasExited)
                Thread.Sleep(10);
            outputWaitHandle.WaitOne(10000);
            errorWaitHandle.WaitOne(10000);
            process.Dispose();
            if (lastResults.Reverse().Take(5).Any(l => l == "true"))
                return;
            Common.Log(lastResults.Join("\r\n"));
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    loaderName,
                    _GetForgelikeInstallerLastLines(lastResults)
                )
            );
        }
    }

    private static JavaEntry? _SelectForgelikeJava(Loading.LoaderTask<bool, bool> task)
    {
        JavaEntry java;
        lock (JavaRuntime.JavaLock)
        {
            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 60));
            if (java is not null)
                return java;

            if (!JavaRuntime.JavaDownloadConfirm(
                    Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));

            var javaLoader = JavaRuntime.GetJavaDownloadLoader();
            try
            {
                javaLoader.Start(17, true);
                while (javaLoader.State == LoadState.Loading && !task.IsAborted)
                    Thread.Sleep(10);
            }
            finally
            {
                javaLoader.Abort();
            }

            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 60));
            if (task.IsAborted)
                return null;
            if (java is null)
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
        }

        return java;
    }

    private static string _BuildForgelikeInstallArguments(
        string target,
        string mcFolder,
        bool useJavaWrapper,
        JavaEntry java)
    {
        string arguments;
        if (useJavaWrapper && !Config.Launch.DisableJlw)
            arguments =
                $"""-Doolloo.jlw.tmpdir="{Common.pathPure.TrimEnd('\\')}" -cp "{Common.pathTemp}Cache\forge_installer.jar;{target}" -jar "{GameLaunch.ExtractJavaWrapper()}" com.bangbang93.ForgeInstaller "{mcFolder}""";
        else
            arguments =
                $"""-cp "{Common.pathTemp}Cache\forge_installer.jar;{target}" com.bangbang93.ForgeInstaller "{mcFolder}""";

        if (java.Installation.MajorVersion >= 9)
            arguments =
                $"--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED {arguments}";

        return arguments;
    }

    private static DataReceivedEventHandler _CreateForgelikeOutputHandler(
        Process process,
        Loading.LoaderTask<bool, bool> task,
        AutoResetEvent waitHandle,
        string loaderName,
        Queue<string> lastResults,
        string readErrorMessage
    )
    {
        return (_, e) =>
        {
            try
            {
                if (e.Data is null)
                {
                    waitHandle.Set();
                }
                else
                {
                    lastResults.Enqueue(e.Data);
                    if (lastResults.Count > 100)
                        lastResults.Dequeue();
                    ForgelikeInjectorLine(e.Data, task);
                }
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
                    Common.Log($"[Installer] 由于任务取消，已中止 {loaderName} 安装");
                    process.Kill();
                }
            }
            catch
            {
                // ignored
            }
        };
    }

    private static string _GetForgelikeInstallerLastLines(Queue<string> lastResults)
    {
        var lastLines = "";
        for (int i = Math.Max(0, lastResults.Count - 5), loopTo = lastResults.Count - 1;
             i <= loopTo;
             i++)
            lastLines += $"\r\n{lastResults.ElementAtOrDefault(i)}";
        return lastLines;
    }

    private static void ForgelikeInjectorLine(string content, Loading.LoaderTask<bool, bool> task)
    {
        switch (content ?? "")
        {
            case "Extracting json":
            {
                Common.Log($"[Installer] {content}");
                task.Progress = 0.07d;
                break;
            }
            case "Downloading libraries":
            {
                Common.Log($"[Installer] {content}");
                task.Progress = 0.08d;
                break;
            }
            case "  File exists: Checksum validated.":
            {
                if (Common.modeDebug)
                    Common.Log($"[Installer] {content}");
                task.Progress += 0.003d;
                break;
            }
            case "Building Processors":
            {
                task.Progress = 0.18d;
                break;
            }
            case "Task: DOWNLOAD_MOJMAPS":
            {
                task.Progress = 0.2d;
                break;
            }
            case "Task: MERGE_MAPPING":
            {
                task.Progress = 0.3d;
                break;
            }
            case "Splitting: ":
            {
                task.Progress = 0.35d;
                break;
            }
            case "Parameter Annotations":
            {
                task.Progress = 0.4d;
                break;
            }
            case "Processing Complete":
            case "log: null":
            {
                task.Progress = 0.5d;
                break;
            }
            case "Sorting":
            {
                task.Progress = 0.65d;
                break;
            }
            case "Remapping final jar":
            {
                task.Progress = 0.72d;
                break;
            }
            case "Remapping jar... 50%":
            {
                task.Progress = 0.76d;
                break;
            }
            case "Remapping jar... 100%":
            {
                task.Progress = 0.81d;
                break;
            }
            case "Injecting profile":
            {
                task.Progress = 0.91d;
                break;
            }

            default:
            {
                if (Common.modeDebug)
                    Common.Log($"[Installer] {content}");
                return;
            }
        }

        Common.Log($"[Installer] {content}");
    }
}