namespace PCL.Platform;

/// <summary>Windows 运行环境检查：推荐 Windows 10 1809 及以上。</summary>
public sealed class WindowsRuntimeEnvironmentService : RuntimeEnvironmentServiceBase
{
    protected override bool IsOutdatedSystem() => !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    protected override bool IsInTempFolder(string executablePath) =>
        base.IsInTempFolder(executablePath)
        || executablePath.Contains(@"AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase);
}
