using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.Utils.OS;

namespace PCL;

internal static class Program
{
    /// <summary>
    /// Program startup point
    /// </summary>
    [STAThread]
    public static void Main()
    {
        // 静默内存优化：以管理员身份启动的辅助进程会在生命周期开始前处理该参数并退出，
        // 退出码为清理的内存量（KB）。必须在此处处理，否则会被单例锁拦截而无法执行。
        var memoryArgs = Basics.CommandLineArguments;
        if (memoryArgs.Length > 0 && memoryArgs[0].StartsWith("--memory"))
        {
            var ramBefore = KernelInterop.GetAvailablePhysicalMemoryBytes();
            try
            {
                MemoryOptimizer.OptimizeCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "内存优化失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            }
            var ramAfter = KernelInterop.GetAvailablePhysicalMemoryBytes();
            Environment.Exit(ramAfter < ramBefore ? 0 : (int)((ramAfter - ramBefore) / 1024));
        }
        if (Basics.CommandLineArguments.Contains("--console")) KernelInterop.AllocateConsole();
#if DEBUG
        if (Basics.CommandLineArguments.Contains("--debug"))
        {
            Console.WriteLine("Waiting for debugger...");
            while (!Debugger.IsAttached) Thread.Sleep(50);
        }
#endif
        Console.WriteLine("Welcome to Plain Craft Launcher 2 Community Edition!");
        // Preloading tasks
        ApplicationService.Loading = static () =>
        {
            var app = new Application();
            app.InitializeComponent();
            return app;
        };
        MainWindowService.Loading = static () =>
        {
            var form = new FormMain();
            return form;
        };
        // From dotnet/wpf #2393: fix tablet devices broken on .NET Core 3.0+
        _ = Tablet.TabletDevices;
        // Start lifecycle
        Lifecycle.OnInitialize();
    }
}