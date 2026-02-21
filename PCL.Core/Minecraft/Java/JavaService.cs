using PCL.Core.App;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using System.Threading.Tasks;
using PCL.Core.App.IoC;

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
            [
            new RegistryJavaScanner(),
            new DefaultPathsScanner(),
            new PathEnvironmentScanner(),
            new MicrosoftStoreJavaScanner(),
            new WhereCommandScanner()
        ]);
        _javaManager.ReadConfig();

        Context.Info("Lookup for local Java...");
        await _javaManager.ScanJavaAsync();

        var logInfo = string.Join("\n\t", _javaManager.GetSortedJavaList());
        Context.Info($"Finished to scan java: \n\t{logInfo}");
    }

    [LifecycleStop]
    private static void _Stop()
    {
        if (_javaManager is null) return;

        _javaManager.SaveConfig();
        _javaManager = null;
    }
}