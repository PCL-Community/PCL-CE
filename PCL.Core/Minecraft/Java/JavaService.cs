using System;
using System.Threading.Tasks;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;

namespace PCL.Core.Minecraft;

[LifecycleService(LifecycleState.Loaded)]
[LifecycleScope("java", "Java 管理")]
public sealed partial class JavaService
{
    private static JavaManager? _javaManager;
    public static JavaManager JavaManager => _javaManager!;

    [LifecycleStart]
    private static async Task _StartAsync()
    {
        if (_javaManager is not null) return;

        Context.Info("Initializing Java Manager...");

        _javaManager = new JavaManager(
            new PeHeaderParser(),
            _CreateScanners());
        _javaManager.ReadConfig();

        Context.Info("Lookup for local Java...");
        await _javaManager.ScanJavaAsync();

        var logInfo = string.Join("\n\t", _javaManager.GetSortedJavaList());
        Context.Info($"Finished to scan java: \n\t{logInfo}");
    }

    /// <summary>
    ///     按当前平台选择 Java 扫描器。Windows 保持原有清单，其余平台改用 PATH 与 Unix JVM 目录。
    /// </summary>
    private static IJavaScanner[] _CreateScanners()
    {
        if (OperatingSystem.IsWindows())
            return
            [
                new RegistryJavaScanner(),
                new DefaultPathsScanner(),
                new PathEnvironmentScanner(),
                new MicrosoftStoreJavaScanner(),
                new WhereCommandScanner()
            ];

        return
        [
            new PathEnvironmentScanner(),
            new UnixJavaScanner()
        ];
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (_javaManager is null) return;

        _javaManager.SaveConfig();
        _javaManager = null;
    }
}