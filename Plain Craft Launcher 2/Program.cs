using System.Diagnostics;
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