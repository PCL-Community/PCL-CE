using System.Diagnostics;
using System.IO;

namespace PCL;

/// <summary>
///     专属文件系统行为。通用文件读写、复制、哈希、解压和目录操作请直接使用 PCL.Core.IO。
/// </summary>
public static class LauncherFileSystem
{
    public static string ResolvePath(string filePath)
    {
        return LauncherPaths.ResolveLauncherFilePath(filePath);
    }

    public static void WaitForFileReady(
        string filePath,
        int timeoutMs = 2000,
        bool requireJson = false)
    {
        filePath = ResolvePath(filePath);
        var start = Environment.TickCount;
        long lastSize = -1;
        while (Environment.TickCount - start < timeoutMs)
        {
            if (File.Exists(filePath))
                try
                {
                    var info = new FileInfo(filePath);
                    var size = info.Length;
                    if (size <= 0)
                        continue;
                    if (!requireJson)
                    {
                        if (size == lastSize)
                            return;
                        lastSize = size;
                    }
                    else
                    {
                        var content = Files.ReadAllTextOrEmptyAsync(filePath).GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(content) && content.Trim().StartsWith("{"))
                            return;
                    }
                }
                catch (IOException)
                {
                }

            Thread.Sleep(50);
        }
    }

    public static void CreateSymbolicLinkByBundledLinkD(string linkPath, string targetPath, int flags)
    {
        using var cmdProcess = new Process();
        var linkDPath = ModLaunch.ExtractLinkD();
        var startInfo = cmdProcess.StartInfo;
        startInfo.FileName = linkDPath;
        startInfo.Arguments = $"\"{linkPath}\" \"{targetPath}\"";
        startInfo.CreateNoWindow = true;
        startInfo.UseShellExecute = false;
        cmdProcess.Start();
        cmdProcess.WaitForExit();
    }
}