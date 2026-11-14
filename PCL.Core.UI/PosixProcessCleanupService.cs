using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Core.UI;

public sealed class PosixProcessCleanupService : IProcessCleanupService
{
    public int CleanupAdditionalData()
    {
        return 0;
    }

    public void DeleteFoldersAndRestart(IReadOnlyList<string> folders)
    {
        var quoted = string.Join(
            " ",
            folders.Select(folder => $"'{folder.Replace("'", "'\\''")}'"));
        var info = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($"sleep 2; rm -rf {quoted}");
        Process.Start(info);
    }
}