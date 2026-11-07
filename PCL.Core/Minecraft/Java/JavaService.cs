using System.Threading.Tasks;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Platform;

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
            PlatformRuntime.Current.Java.CreateParser(),
            _CreateScanners());
        _javaManager.ReadConfig();

        Context.Info("Lookup for local Java...");
        await _javaManager.ScanJavaAsync();

        var logInfo = string.Join("\n\t", _javaManager.GetSortedJavaList());
        Context.Info($"Finished to scan java: \n\t{logInfo}");
    }

    private static IJavaScanner[] _CreateScanners()
    {
        return PlatformRuntime.Current.Java.CreateScanners();
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (_javaManager is null) return;

        _javaManager.SaveConfig();
        _javaManager = null;
    }
}