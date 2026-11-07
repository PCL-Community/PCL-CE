using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Core.App;

/// <summary>使用系统 opener 的 POSIX Shell 启动器。</summary>
public sealed class PosixShellLauncher(string command) : IShellLauncher
{
    public void OpenPath(string path, string? workingDirectory = null, string? arguments = null)
    {
        _Start(command, path, workingDirectory, arguments);
    }

    public void OpenUrl(string url)
    {
        _Start(command, url, null);
    }

    private static void _Start(
        string opener,
        string target,
        string? workingDirectory,
        string? arguments = null)
    {
        var startInfo = new ProcessStartInfo(opener)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Basics.CurrentDirectory
        };
        startInfo.ArgumentList.Add(target);
        if (!string.IsNullOrEmpty(arguments))
            startInfo.ArgumentList.Add(arguments);
        Process.Start(startInfo);
    }
}