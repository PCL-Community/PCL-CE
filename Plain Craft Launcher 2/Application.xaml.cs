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
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class Application
{
    public static readonly List<Border> showingTooltips = new();

    public Application()
    {
        // 注册生命周期事件
        Lifecycle.When(LifecycleState.Loaded, Application_Startup);
        SessionEnding += Application_SessionEnding;
    }

    // 开始
    private void Application_Startup() // (sender As Object, e As StartupEventArgs) Handles Me.Startup
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
            {
                if (args[0] == "--gpu")
                {
                    // 调整显卡设置
                    try
                    {
                        ModMain.SetGPUPreference(args[1].Trim('"'));
                        Environment.Exit((int)ModBase.ProcessReturnValues.TaskDone);
                    }
                    catch (Exception ex)
                    {
                        Environment.Exit((int)ModBase.ProcessReturnValues.Fail);
                    }
                }
            }

            // 初始化文件结构
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Pictures");
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Musics");
            Directory.CreateDirectory(Path.Combine(ModBase.pathTemp, "Cache"));
            Directory.CreateDirectory(Path.Combine(ModBase.pathTemp, "Download"));
            Directory.CreateDirectory(ModBase.pathAppdata);
            // 设置 ToolTipService 默认值
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(300));
            ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(400));
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(9999999));
            ToolTipService.PlacementProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(PlacementMode.Bottom));
            ToolTipService.HorizontalOffsetProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(8.0d));
            ToolTipService.VerticalOffsetProperty.OverrideMetadata(typeof(DependencyObject),
                new FrameworkPropertyMetadata(4.0d));
            // 设置初始窗口
            if (Config.Preference.ShowStartupLogo)
            {
                ModMain.frmStart = new SplashScreen(@"Images\icon.ico");
                ModMain.frmStart.Show(false, true);
            }

            // 检测异常环境
            var problemList = new List<string>();
            var currentOSVersion = NtInterop.GetCurrentOsVersion();
            if (currentOSVersion.Build < 17763)
                problemList.Add(Lang.Text("Application.EnvironmentWarning.WindowsVersion"));
            if (SystemInfo.Is32BitSystem)
                problemList.Add(Lang.Text("Application.EnvironmentWarning.System32Bit"));
            if (ModBase.exePath.Contains(Path.GetTempPath()) || ModBase.exePath.Contains(@"AppData\Local\Temp\"))
                problemList.Add(Lang.Text("Application.EnvironmentWarning.TempFolder"));
            if (ModBase.exePath.ContainsF("wechat_files", true) || ModBase.exePath.ContainsF("WeChat Files", true) ||
                ModBase.exePath.ContainsF("Tencent Files", true))
                problemList.Add(Lang.Text("Application.EnvironmentWarning.SocialSoftwareFolder"));
            if (problemList.Count != 0)
                ModMain.MyMsgBox(
                    Lang.Text("Application.EnvironmentWarning.Message", problemList.Join("\r\n")),
                    Lang.Text("Application.EnvironmentWarning.Title"),
                    Lang.Text("Application.EnvironmentWarning.IKnow"),
                    IsWarn: true);
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
                updateBranchCfg.SetValue(ModBase.versionBaseName.Contains("beta")
                    ? Core.App.UpdateChannel.Beta
                    : Core.App.UpdateChannel.Release);
            // 删除旧日志
            for (var i = 1; i <= 5; i++)
            {
                var oldLogFile = $@"{ModBase.exePath}PCL\Log-CE{i}.log";
                if (File.Exists(oldLogFile))
                    File.Delete(oldLogFile);
            }

            // 计时
            ModBase.Log("[Start] 第一阶段加载用时：" + (TimeUtils.GetTimeTick() - ModBase.applicationStartTick) + " ms");
            ModBase.applicationStartTick = TimeUtils.GetTimeTick();
            ModAnimation.AniControlEnabled += 1;
        }
        catch (Exception ex)
        {
            var filePath = Basics.ExecutablePath;
            MessageBox.Show(ex + "\r\n" + Lang.Text("Application.InitializationError.Path",
                    string.IsNullOrEmpty(filePath) ? Lang.Text("Application.InitializationError.PathUnavailable") : filePath),
                Lang.Text("Application.InitializationError.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            FormMain.EndProgramForce(ModBase.ProcessReturnValues.Exception);
        }
    }

    // 结束
    private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        ModMain.frmMain.EndProgram(false);
    }

// Error handling for unhandled exceptions
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            e.Handled = true;
            if (ModBase.isProgramEnded) return;

            ModBase.FeedbackInfo();

            var detail = e.Exception.ToString();

            // Automatic error analysis for environment issues
            if (detail.Contains("System.Windows.Threading.Dispatcher.Invoke") ||
                detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") ||
                detail.Contains("未能加载文件或程序集"))
            {
                ModBase.OpenWebsite("https://get.dot.net/8");
                LogWrapper.Error(e.Exception,
                    "Your .NET Desktop Runtime is outdated or corrupted. Please reinstall .NET 8!");
            }
            else
            {
                LogWrapper.Error(e.Exception, "An unexpected error occurred");
            }
        }
        catch
        {
            // Equivalent to On Error Resume Next for safety in the global handler
        }
    }

    // Win32 API declaration for DLL directory configuration
    [DllImport("kernel32", EntryPoint = "SetDllDirectoryA", CharSet = CharSet.Ansi)]
    private static extern bool SetDllDirectory(string lpPathName);
    // 切换窗口

    // 控件模板事件
    private void MyIconButton_Click(object sender, EventArgs e)
    {
    }

    private void TooltipLoaded(object sender, EventArgs e)
    {
        showingTooltips.Add((Border)sender);
    }

    private void TooltipUnloaded(object sender, RoutedEventArgs e)
    {
        showingTooltips.Remove((Border)sender);
    }

    // 自定义监听器类
    public class BindingErrorTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            ModBase.Log($"警告，检测到 Binding 失败：{message}");
        }

        public override void WriteLine(string message)
        {
            ModBase.Log($"警告，检测到 Binding 失败：{message}");
        }
    }
}
