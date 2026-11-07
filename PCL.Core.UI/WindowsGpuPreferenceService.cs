using Microsoft.Win32;
using PCL.Core.Platform;

namespace PCL.Core.UI;

public sealed class WindowsGpuPreferenceService : IGpuPreferenceService
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public bool IsSupported => true;

    public void Set(string javaExecutablePath, bool preferHighPerformance)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key?.SetValue(
            javaExecutablePath,
            preferHighPerformance ? "GpuPreference=2;" : "GpuPreference=0;",
            RegistryValueKind.String);
    }
}