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
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public partial class Application
{
    public static readonly List<Border> ShowingTooltips = new();

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
                    /* TODO ERROR: Skipped IfDirectiveTrivia
                    #If DEBUGRESERVED Then
                    */ /* TODO ERROR: Skipped DisabledTextTrivia
                                        '制作更新包
                                    ElseIf args(0) = "--edit1" Then
                                        ExeEdit(args(1), True)
                                        Environment.Exit(ProcessReturnValues.TaskDone)
                                    ElseIf args(0) = "--edit2" Then
                                        ExeEdit(args(1), False)
                                        Environment.Exit(ProcessReturnValues.TaskDone)
                    */ /* TODO ERROR: Skipped EndIfDirectiveTrivia
                    #End If
                    */
            }

            // 初始化文件结构
            Directory.CreateDirectory(ModBase.ExePath + @"PCL\Pictures");
            Directory.CreateDirectory(ModBase.ExePath + @"PCL\Musics");
            Directory.CreateDirectory(ModBase.PathTemp + "Cache");
            Directory.CreateDirectory(ModBase.PathTemp + "Download");
            Directory.CreateDirectory(ModBase.PathAppdata);
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If False Then
            */ /* TODO ERROR: Skipped DisabledTextTrivia
                        '检测单例
                        Dim ShouldWaitForExit As Boolean = args.Length > 0 AndAlso args(0) = "--wait" '要求等待已有的 PCL 退出
                        Dim WaitRetryCount As Integer = 0
            WaitRetry:
                        Dim WindowHwnd As IntPtr = FindWindow(Nothing, "Plain Craft Launcher Community Edition ")
                        If WindowHwnd = IntPtr.Zero Then FindWindow(Nothing, "Plain Craft Launcher 2 Community Edition ")
                        If WindowHwnd <> IntPtr.Zero Then
                            If ShouldWaitForExit AndAlso WaitRetryCount < 20 Then '至多等待 10 秒
                                WaitRetryCount += 1
                                Thread.Sleep(500)
                                GoTo WaitRetry
                            End If
                            '将已有的 PCL 窗口拖出来
                            ShowWindowToTop(WindowHwnd)
                            '播放提示音并退出
                            Beep()
                            Environment.[Exit](ProcessReturnValues.Cancel)
                        End If
            */ /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */ // 设置 ToolTipService 默认值
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
                ModMain.FrmStart = new SplashScreen(@"Images\icon.ico");
                ModMain.FrmStart.Show(false, true);
            }

            // 检测异常环境
            var problemList = new List<string>();
            var currentOSVersion = NtInterop.GetCurrentOsVersion();
            if (currentOSVersion.Build < 17763)
                problemList.Add("- Windows 版本不满足推荐要求，推荐至少 Windows 10 1809，建议考虑升级 Windows 系统");
            if (ModBase.Is32BitSystem)
                problemList.Add("- 当前系统为 32 位，不受 PCL 和新版 Minecraft 支持，非常建议重装为 64 位系统后再进行游戏");
            if (ModBase.ExePath.Contains(Path.GetTempPath()) || ModBase.ExePath.Contains(@"AppData\Local\Temp\"))
                problemList.Add("- PCL 正在临时目录运行，请将 PCL 从压缩包中解压之后再使用，否则可能导致游戏存档或设置丢失");
            if (ModBase.ExePath.ContainsF("wechat_files", true) || ModBase.ExePath.ContainsF("WeChat Files", true) ||
                ModBase.ExePath.ContainsF("Tencent Files", true))
                problemList.Add("- PCL 正在 QQ、微信、TIM 等社交软件的下载目录运行，请考虑移动到其他位置，否则可能导致游戏存档或设置丢失");
            if (problemList.Count != 0)
                ModMain.MyMsgBox(
                    "PCL CE 在启动时检测到环境问题：" + "\r\n" + "\r\n" + problemList.Join("\r\n") +
                    "\r\n" + "\r\n" + "不解决这些问题可能会导致部分功能无法正常工作……", "环境警告", "我知道了", IsWarn: true);
            // 设置初始化
            ModBase.Setup.Load("SystemDebugMode");
            ModBase.Setup.Load("SystemDebugAnim");
            ModBase.Setup.Load("SystemHttpProxy");
            ModBase.Setup.Load("SystemHttpProxyCustomUsername");
            ModBase.Setup.Load("SystemHttpProxyType");
            ModBase.Setup.Load("ToolDownloadThread");
            ModBase.Setup.Load("ToolDownloadSpeed");
            ModBase.Setup.Load("UiFont");
            var updateBranchCfg = Config.Update.UpdateChannelConfig;
            if (updateBranchCfg.IsDefault())
                updateBranchCfg.SetValue(ModBase.VersionBaseName.Contains("beta")
                    ? Core.App.UpdateChannel.Beta
                    : Core.App.UpdateChannel.Release);
            // 删除旧日志
            for (var i = 1; i <= 5; i++)
            {
                var oldLogFile = $@"{ModBase.ExePath}PCL\Log-CE{i}.log";
                if (File.Exists(oldLogFile))
                    File.Delete(oldLogFile);
            }

            // 计时
            ModBase.Log("[Start] 第一阶段加载用时：" + (TimeUtils.GetTimeTick() - ModBase.ApplicationStartTick) + " ms");
            ModBase.ApplicationStartTick = TimeUtils.GetTimeTick();
            // 执行测试
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If DEBUGRESERVED Then
            */ /* TODO ERROR: Skipped DisabledTextTrivia
                        Test()
            */ /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            ModAnimation.AniControlEnabled += 1;
        }
        catch (Exception ex)
        {
            var FilePath = ModBase.ExePathWithName;
            MessageBox.Show(ex + "\r\n" + "PCL 所在路径：" + (string.IsNullOrEmpty(FilePath) ? "获取失败" : FilePath),
                "PCL 初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            FormMain.EndProgramForce(ModBase.ProcessReturnValues.Exception);
        }
    }

    // 结束
    private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        ModMain.FrmMain.EndProgram(false);
    }

// Error handling for unhandled exceptions
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            e.Handled = true;
            if (ModBase.IsProgramEnded) return;

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
        ShowingTooltips.Add((Border)sender);
    }

    private void TooltipUnloaded(object sender, RoutedEventArgs e)
    {
        ShowingTooltips.Remove((Border)sender);
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
