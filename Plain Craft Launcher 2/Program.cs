using System.Diagnostics;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.Utils.OS;
using PCL.Core.Wpf;

namespace PCL;

internal static class Program
{
    /// <summary>
    ///     Program startup point
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
            return new WpfApplicationHost(app);
        };
        // 注册 Core 弹窗服务的界面呈现器
        DialogPresenter.Install();
        HintPresenter.Install();
        MainWindowService.Loading = static () => new WpfWindowHost(new FormMain());
        // From dotnet/wpf #2393: fix tablet devices broken on .NET Core 3.0+

        _ = Tablet.TabletDevices;
        // PCL.Core 通过 PlatformServices 与 UI 解耦，需在生命周期启动前注册 WPF 实现
        PlatformRegistration.Register();
        // PCL.Core.Wpf 中的生命周期服务不在 PCL.Core 的编译单元内，需要显式注册
        LifecycleRegistration.Register();
        // 加载框架的 UI 通知由 WPF 层接管（任务栏、下载按钮、左侧任务列表）
        LoadingUi.Current = new LoadingUiBridge();
        LauncherUi.Interaction = new WpfUserInteraction();
        DownloadUserInteraction.Current = new WpfDownloadInteraction();
        UpdateUi.Current = new WpfUpdateUi();
        AnnouncementActions.Current = new WpfAnnouncementActions();
        WatcherUi.Current = new WpfWatcherUi();
        MediaService.Current = new WpfMediaService();
        CrashAnalysisService.Current = new WpfCrashAnalysisService();
        CompNavigation.Current = new WpfCompNavigationService();
        CompUi.Current = new WpfCompUi();
        CompLocalUi.Current = new WpfCompLocalUi();
        MemorySettings.Current = new WpfMemorySettings();
        LaunchUi.Current = new WpfLaunchUi();
        LoginProfile.Current = new WpfProfileUi();
        LogFilter.Current = new WpfLogFilter();
        RestrictedFeatures.Current = new WpfRestrictedFeatures();
        ModpackUi.Current = new WpfModpackUi();
        EventUi.Current = new WpfEventUi();
        LauncherState.InstanceSelectionChanged += _ => PageDownloadCompDetail.cachedFolder.Clear();

        // 业务层日志的 UI 提示桥接：Launcher.Core 只负责记录，弹窗/提示仍由 WPF 层处理
        LauncherCommon.UiLogHandler = static (ex, text, level, title, userSummary) =>
        {
            if (ex is null) Common.Log(text, (Common.LogLevel)level, title, userSummary);
            else Common.Log(ex, text, (Common.LogLevel)level, title, userSummary);
        };
        // Start lifecycle
        Lifecycle.OnInitialize();
    }
}