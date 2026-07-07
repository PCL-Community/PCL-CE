using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Controls;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class Application
{
    public Application()
    {
        // 注册生命周期事件
        Lifecycle.When(LifecycleState.Loaded, _ApplicationStartup);
        Lifecycle.When(LifecycleState.WindowCreated, _ShowEnvironmentWarning);
        SessionEnding += _ApplicationSessionEnding;
    }

    // 开始
    private static void _ApplicationStartup()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 创建自定义跟踪监听器，用于检测是否存在 Binding 失败
            PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            StartupValidation.EnsureWpfFont();

            // 检查参数调用
            var args = Basics.CommandLineArguments;
            if (args.Length > 0)
                if (args[0] == "--gpu")
                    // 调整显卡设置
                    try
                    {
                        ModMain.SetGPUPreference(args[1].Trim('"'));
                        Environment.Exit((int)LauncherExitCode.TaskDone);
                    }
                    catch (Exception)
                    {
                        Environment.Exit((int)LauncherExitCode.Fail);
                    }

            // 初始化文件结构
            Directory.CreateDirectory(LauncherPaths.ExecutableDirectoryWithSlash + @"PCL\Pictures");
            Directory.CreateDirectory(LauncherPaths.ExecutableDirectoryWithSlash + @"PCL\Musics");
            Directory.CreateDirectory(Path.Combine(LauncherPaths.TempWithSlash, "Cache"));
            Directory.CreateDirectory(Path.Combine(LauncherPaths.TempWithSlash, "Download"));
            Directory.CreateDirectory(LauncherPaths.LegacyAppDataWithSlash);

            // 设置 ToolTipService 默认值
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(100));
            Tooltip.Enable();

            // 设置初始窗口
            if (Config.Preference.ShowStartupLogo)
            {
                ModMain.frmStart = new SplashScreen(@"Images\icon.ico");
                ModMain.frmStart.Show(false, true);
            }

            // 设置初始化
            _ = Config.Debug.Enabled;
            _ = Config.Debug.AnimationSpeed;
            _ = Config.Network.HttpProxy.CustomAddress;
            _ = Config.Network.HttpProxy.CustomUsername;
            _ = Config.Network.HttpProxy.Type;
            _ = Config.Download.ThreadLimit;
            _ = Config.Download.SpeedLimit;
            _ = Config.Preference.Font;
            var updateBranchCfg = Config.Update.UpdateChannelConfig;
            if (updateBranchCfg.IsDefault())
                updateBranchCfg.SetValue(LauncherEnvironment.VersionBaseName.Contains("beta")
                    ? Core.App.UpdateChannel.Beta
                    : Core.App.UpdateChannel.Release);

            // 删除旧日志
            for (var i = 1; i <= 5; i++)
            {
                var oldLogFile = $@"{LauncherPaths.ExecutableDirectoryWithSlash}PCL\Log-CE{i}.log";
                if (File.Exists(oldLogFile))
                    File.Delete(oldLogFile);
            }

            // 计时
            LauncherLog.Log("[Start] 第一阶段加载用时：" + (TimeUtils.GetTimeTick() - LauncherRuntime.ApplicationStartTick) +
                            " ms");
            LauncherRuntime.ApplicationStartTick = TimeUtils.GetTimeTick();
            ModAnimation.AniControlEnabled += 1;
        }
        catch (Exception ex)
        {
            var filePath = Basics.ExecutablePath;
            var summary = Lang.Text("Application.InitializationError.Path",
                string.IsNullOrEmpty(filePath)
                    ? Lang.Text("Application.InitializationError.PathUnavailable")
                    : filePath);
            MessageBox.Show(
                ExceptionDetails.Compose(summary, ex),
                Lang.Text("SystemDialog.Startup.InitializationTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            FormMain.EndProgramForce(LauncherExitCode.Exception);
        }
    }

    // 检测异常环境
    private static void _ShowEnvironmentWarning()
    {
        var problemList = new List<string>();
        var currentOsVersion = NtInterop.GetCurrentOsVersion();
        if (currentOsVersion.Build < 17763)
            problemList.Add(Lang.Text("Application.EnvironmentWarning.WindowsVersion"));
        if (SystemInfo.Is32BitSystem)
            problemList.Add(Lang.Text("Application.EnvironmentWarning.System32Bit"));
        if (LauncherPaths.ExecutableDirectoryWithSlash.Contains(Path.GetTempPath()) ||
            LauncherPaths.ExecutableDirectoryWithSlash.Contains(@"AppData\Local\Temp\"))
            problemList.Add(Lang.Text("Application.EnvironmentWarning.TempFolder"));
        if (LauncherPaths.ExecutableDirectoryWithSlash.ContainsF("wechat_files", true) ||
            LauncherPaths.ExecutableDirectoryWithSlash.ContainsF("WeChat Files", true) ||
            LauncherPaths.ExecutableDirectoryWithSlash.ContainsF("Tencent Files", true))
            problemList.Add(Lang.Text("Application.EnvironmentWarning.SocialSoftwareFolder"));
        if (problemList.Count == 0) return;

        ModMain.MyMsgBox(
            Lang.Text("Application.EnvironmentWarning.Message", problemList.Join("\r\n")),
            Lang.Text("Application.EnvironmentWarning.Title"),
            Lang.Text("Application.EnvironmentWarning.IKnow"),
            isWarn: true);
    }

    // 结束
    private static void _ApplicationSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        ModMain.frmMain.EndProgram(false);
    }

    /**
     * Error handling for unhandled exceptions
     */
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            e.Handled = true;
            if (LauncherRuntime.IsProgramEnded) return;

            LauncherFeedbackService.FeedbackInfo();

            var detail = e.Exception.ToString();

            // Automatic error analysis for environment issues
            if (detail.Contains("System.Windows.Threading.Dispatcher.Invoke") ||
                detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") ||
                detail.Contains("未能加载文件或程序集"))
            {
                LauncherProcess.OpenWebsite("https://get.dot.net/10");
                LogWrapper.Error(
                    e.Exception,
                    Lang.Text("SystemDialog.Startup.DotNetRuntimeOutdated.Message"));
            }
            else
            {
                LogWrapper.Error(e.Exception, Lang.Text("SystemDialog.Error.Unexpected.Message"));
            }
        }
        catch
        {
            // Equivalent to On Error Resume Next for safety in the global handler
        }
    }

    // Win32 API declaration for DLL directory configuration
    [DllImport("kernel32", EntryPoint = "SetDllDirectoryA", CharSet = CharSet.Ansi)]
    private static extern bool _SetDllDirectory(string lpPathName);
    // 切换窗口

    // 控件模板事件
    private void _MyIconButtonClick(object sender, EventArgs e)
    {
    }

    // 自定义监听器类
    public class BindingErrorTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            LauncherLog.Log($"警告，检测到 Binding 失败：{message}");
        }

        public override void WriteLine(string message)
        {
            LauncherLog.Log($"警告，检测到 Binding 失败：{message}");
        }
    }
}