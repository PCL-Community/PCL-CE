using System.Diagnostics;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

/// <summary>普通进程启动服务。</summary>
public sealed class PlatformProcessLauncher : IProcessLauncher
{
    public Process? Start(string fileName, string? arguments = null)
    {
        return ProcessInterop.Start(fileName, arguments);
    }
}