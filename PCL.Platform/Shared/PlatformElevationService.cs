using System.Diagnostics;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

/// <summary>使用平台底层进程互操作实现提权。</summary>
public sealed class PlatformElevationService : IElevationService
{
    public bool IsElevated => ProcessInterop.IsAdmin();

    public Process? StartElevated(string fileName, string? arguments = null)
    {
        return ProcessInterop.Start(fileName, arguments, true);
    }
}