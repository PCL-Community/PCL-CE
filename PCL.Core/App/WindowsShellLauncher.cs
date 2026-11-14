using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PCL.Core.Platform;

namespace PCL.Core.App;

public sealed class WindowsShellLauncher : IShellLauncher
{
    public void OpenPath(string path, string? workingDirectory = null, string? arguments = null)
    {
        var psi = new ProcessStartInfo(path)
        {
            WorkingDirectory = workingDirectory ?? Basics.CurrentDirectory,
            UseShellExecute = true,
            CreateNoWindow = true,
            Arguments = arguments ?? string.Empty
        };
        try
        {
            Process.Start(psi);
        }
        catch (Win32Exception ex)
            when (ex.NativeErrorCode == 1155 && File.Exists(path))
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
        }
    }

    public void OpenUrl(string url)
    {
        OpenPath(url);
    }
}