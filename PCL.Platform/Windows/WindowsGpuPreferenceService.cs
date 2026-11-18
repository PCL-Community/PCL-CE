using Microsoft.Win32;
using PCL.Core.Platform;

namespace PCL.Platform;

public sealed class WindowsGpuPreferenceService : IGpuPreferenceService
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public bool IsSupported => true;

    public void Set(string javaExecutablePath, bool preferHighPerformance)
    {
        var executable = Path.GetFullPath(javaExecutablePath);
        if (!File.Exists(executable))
            throw new FileNotFoundException("Java executable does not exist.", executable);

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key?.SetValue(
            executable,
            preferHighPerformance ? "GpuPreference=2;" : "GpuPreference=0;",
            RegistryValueKind.String);
    }
}