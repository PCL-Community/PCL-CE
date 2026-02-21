using System.Diagnostics;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;

namespace PCL;

internal static class Program
{
    /// <summary>
    ///     Program startup point
    /// </summary>
    [STAThread]
    public static void Main()
    {
        /* TODO ERROR: Skipped IfDirectiveTrivia
        #If DEBUG
        */
        if (Basics.CommandLineArguments.Contains("--debug"))
            while (!Debugger.IsAttached)
                Thread.Sleep(50);
        /* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
        Console.WriteLine("Welcome to Plain Craft Launcher 2 Community Edition!");
        // Preloading tasks
        ApplicationService.Loading = () =>
        {
            var app = new Application();
            app.InitializeComponent();
            return app;
        };
        MainWindowService.Loading = () =>
        {
            var form = new FormMain();
            return form;
        };
        // From dotnet/wpf #2393: fix tablet devices broken on .NET Core 3.0+
        // ReSharper disable once UnusedVariable
        var vbSucks = Tablet.TabletDevices;
        // Start lifecycle
        Lifecycle.OnInitialize();
    }
}