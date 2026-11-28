namespace PCL.Platform;

/// <summary>Windows 运行环境检查</summary>
public sealed class WindowsRuntimeEnvironmentService : RuntimeEnvironmentServiceBase
{
    // 推荐至少 Windows 10 21H2 (19044)
    protected override bool IsOutdatedSystem() => !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19044);

    protected override bool IsInTempFolder(string executablePath) =>
        base.IsInTempFolder(executablePath)
        || executablePath.Contains(@"AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase);
}
