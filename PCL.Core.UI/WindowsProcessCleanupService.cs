using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Core.UI;

public sealed class WindowsProcessCleanupService : IProcessCleanupService
{
    public int CleanupAdditionalData()
    {
        var path = Path.Combine(
            Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
            "ProgramData",
            "PCL");
        return !Directory.Exists(path) ? 0 : DeleteDirectory(path);
    }

    public void DeleteFoldersAndRestart(IReadOnlyList<string> folders)
    {
        var arguments = folders.Aggregate(
            "/c timeout /t 5 /nobreak >nul & ",
            (current, folder) => current + $"rmdir /s /q \"{folder}\" & ");
        Process.Start(
            new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });
    }

    private static int DeleteDirectory(string path)
    {
        try
        {
            var count = Directory
                    .EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
                    .Count()
                + 1;
            Directory.Delete(path, true);
            return count;
        }
        catch
        {
            return 0;
        }
    }
}