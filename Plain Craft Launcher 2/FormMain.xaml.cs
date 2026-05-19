using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Theme;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Validate;
using PCL.Network;

namespace PCL;

public partial class FormMain
{
    // 愚人节鼠标位置
    public MouseEventArgs lastMouseArg;

    private void FormMain_MouseMove(object sender, MouseEventArgs e)
    {
        lastMouseArg = e;
    }

    #region 基础

    // 更新日志
    private void ShowUpdateLog()
    {
        ModBase.RunInNewThread(() =>
        {
            var ChangelogFile = $"{ModBase.PathTemp}CEUpdateLog.md";
            string Changelog;
            if (File.Exists(ChangelogFile))
                Changelog = ModBase.ReadFile(ChangelogFile);
            else
                Changelog = "欢迎使用呀~";
            if (ModMain.MyMsgBoxMarkdown(Changelog,
                    "PCL CE 已更新至 " + ModBase.VersionBranchName + " " + ModBase.VersionBaseName, "确定", "完整更新日志") ==
                2) ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases");
        }, "UpdateLog Output");
    }

    // 窗口加载
    private bool IsWindowLoadFinished;
    private readonly DragHelper _helper = new();

    public FormMain()
    {
        ModBase.ApplicationStartTick = TimeUtils.GetTimeTick();
        // 刷新主题
        // ThemeCheckAll(False)
        // ThemeRefreshColor()
        ThemeService.ColorModeChanged += (_, _) => ThemeManager.ThemeRefresh();
        ThemeService.ColorThemeChanged += theme => ThemeManager.ThemeRefresh((int)theme);
        // 窗体参数初始化
        ModMain.FrmMain = this;
        ModMain.FrmLaunchLeft = new PageLaunchLeft();
        ModMain.FrmLaunchRight = new PageLaunchRight();
        // 版本号改变
        var LastVersion = States.System.LastVersion;
        if (LastVersion < ModBase.VersionCode)
        {
            // 重新询问是否启用遥测数据收集
            if (LastVersion <= 511)
            {
                if (!Config.System.TelemetryConfig.IsDefault() && Config.System.Telemetry)
                {
                    Config.System.TelemetryConfig.Reset();
                    ModBase.Log("[Start] 遥测策略变更：由旧版本升级到含新版遥测的版本，已重置遥测设置");
                }
            }
            // 触发升级
            UpgradeSub(LastVersion);
        }
        else if (LastVersion > ModBase.VersionCode)
            // 触发降级
            DowngradeSub(LastVersion);
        // 版本隔离设置迁移
        if (ModBase.Setup.IsUnset("LaunchArgumentIndieV2"))
        {
            if (!ModBase.Setup.IsUnset("LaunchArgumentIndie"))
            {
                ModBase.Log("[Start] 从老 PCL 迁移版本隔离");
                Config.Launch.IndieSolutionV2 = Config.Launch.IndieSolutionV1;
            }
            else if (!ModBase.Setup.IsUnset("WindowHeight"))
            {
                ModBase.Log("[Start] 从老 PCL 升级，但此前未调整版本隔离，使用老的版本隔离默认值");
                Config.Launch.IndieSolutionV2Config.Reset(Config.Launch.IndieSolutionV1Config.DefaultValue);
            }
            else
            {
                ModBase.Log("[Start] 全新的 PCL，使用新的版本隔离默认值");
                Config.Launch.IndieSolutionV2Config.Reset(Config.Launch.IndieSolutionV2Config.DefaultValue);
            }
        }

        ModBase.Setup.Load("UiLauncherTheme");
        // 注册拖拽事件（不能直接加 Handles，否则没用；#6340）
        AddHandler(DragDrop.DragEnterEvent, new DragEventHandler(HandleDrag), true);
        AddHandler(DragDrop.DragOverEvent, new DragEventHandler(HandleDrag), true);
        // 注册 MsgBox 事件
        MsgBoxWrapper.OnShow += ModMain.MsgBoxWrapper_OnShow;
        // 注册 Hint 事件
        HintWrapper.OnShow += ModMain.HintWrapper_OnShow;
        // 加载 UI
        InitializeComponent();
        Opacity = 0d;
        try
        {
            Height = States.UI.WindowHeight;
            Width = States.UI.WindowWidth;
        }
        catch (Exception ex) // 修复 #2019
        {
            ModBase.Log(ex, "读取窗口默认大小失败", ModBase.LogLevel.Hint);
            Height = MinHeight + 100d;
            Width = MinWidth + 100d;
        }

        // 管理员权限下文件拖拽
        if (ProcessInterop.IsAdmin())
        {
            ModBase.Log("[Start] PCL 当前正以管理员权限运行");
            SourceInitialized += (_, _) =>
            {
                var windowInterop = new WindowInteropHelper(this);
                _helper.HwndSource = HwndSource.FromHwnd(windowInterop.Handle);
                _helper.AddHook();
            };
            Closing += (_, _) => _helper.RemoveHook();
            _helper.DragDrop += (_, _) => FileDrag(_helper.DropFilePaths);
        }

        if (!(ModMain.FrmLaunchLeft.Parent == null))
            ModMain.FrmLaunchLeft.SetValue(ContentPresenter.ContentProperty, null);
        if (!(ModMain.FrmLaunchRight.Parent == null))
            ModMain.FrmLaunchRight.SetValue(ContentPresenter.ContentProperty, null);
        PanMainLeft.Child = ModMain.FrmLaunchLeft;
        PageLeft = ModMain.FrmLaunchLeft;
        PanMainRight.Child = ModMain.FrmLaunchRight;
        PageRight = ModMain.FrmLaunchRight;
        ModMain.FrmLaunchRight.PageState = MyPageRight.PageStates.ContentStay;
        // 调试模式提醒
        if (ModBase.ModeDebug)
            ModMain.Hint("[调试模式] PCL 正以调试模式运行，这可能会导致性能下降，若无必要请不要开启！");
        // 尽早执行的加载池
        ModMinecraft.McFolderListLoader
            .Start(0); // 为了让下载已存在文件检测可以正常运行，必须跑一次；为了让启动按钮尽快可用，需要尽早执行；为了与 PageLaunchLeft 联动，需要为 0 而不是 GetUuid

        ModBase.Log("[Start] 第二阶段加载用时：" + (TimeUtils.GetTimeTick() - ModBase.ApplicationStartTick) + " ms");
        // 注册生命周期状态事件
        Lifecycle.When(LifecycleState.WindowCreated, FormMain_Loaded);
    }

    private void FormMain_Loaded() // (sender As Object, e As RoutedEventArgs) Handles Me.Loaded
    {
        FormMain_SizeChanged();
        ModBase.ApplicationStartTick = TimeUtils.GetTimeTick();
        ModBase.FrmHandle = new WindowInteropHelper(this).Handle;
        // 读取设置
        ModBase.Setup.Load("UiBackgroundOpacity");
        ModBase.Setup.Load("UiBackgroundBlur");
        ModBase.Setup.Load("UiLogoType");
        ModBase.Setup.Load("UiHiddenPageDownload");
        ModBase.Setup.Load("UiAutoPauseVideo"); // 智能暂停视频背景
        PageSetupUI.HiddenRefresh();
        PageSetupUI.BackgroundRefresh(false, true);
        ModMusic.MusicRefreshPlay(false, true);
        // 扩展按钮
        BtnExtraUpdateRestart.ShowCheck = BtnExtraUpdateRestart_ShowCheck;
        BtnExtraDownload.ShowCheck = BtnExtraDownload_ShowCheck;
        BtnExtraBack.ShowCheck = BtnExtraBack_ShowCheck;
        BtnExtraApril.ShowCheck = BtnExtraApril_ShowCheck;
        BtnExtraShutdown.ShowCheck = BtnExtraShutdown_ShowCheck;
        BtnExtraLog.ShowCheck = BtnExtraLog_ShowCheck;
        BtnExtraApril.ShowRefresh();
        // 初始化尺寸改变
        if (!Config.Preference.LockWindowSize)
            AddResizer();
        else
            RemoveResizer();
        // PLC 彩蛋
        if (RandomUtils.NextInt(1, 1000) == 233)
            ShapeTitleLogo.Data = (Geometry)new GeometryConverter().ConvertFromString(
                "M26,29 v-25 h6 a7,7 180 0 1 0,14 h-6 M83,6.5 a10,11.5 180 1 0 0,18 M48,2.5 v24.5 h13.5");
        // 加载窗口

        ThemeManager.ThemeRefresh();

        Lifecycle.CurrentApplication.Resources["BlurSamplingRate"] = Config.Preference.Blur.SamplingRate * 0.01d;
        Lifecycle.CurrentApplication.Resources["BlurType"] = Config.Preference.Blur.KernelType;
        if (Config.Preference.Blur.IsEnabled)
            Lifecycle.CurrentApplication.Resources["BlurRadius"] = Config.Preference.Blur.Radius * 1.0d;
        else
            Lifecycle.CurrentApplication.Resources["BlurRadius"] = 0.0d;

        // #If DEBUG Then
        // MinHeight = 50
        // MinWidth = 50
        // #End If
        Topmost = false;
        if (ModMain.FrmStart is not null)
            ModMain.FrmStart.Close(new TimeSpan(0, 0, 0, 0, (int)Math.Round(400d / ModAnimation.AniSpeed)));
        // 更改窗口
        // Top = (GetWPFSize(My.Computer.Screen.WorkingArea.Height) - Height) / 2
        // Left = (GetWPFSize(My.Computer.Screen.WorkingArea.Width) - Width) / 2
        IsSizeSaveable = true;
        ShowWindowToTop();
        var HwndSource = (HwndSource)PresentationSource.FromVisual(this);
        HwndSource.AddHook(WndProc);
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() => ModAnimation.AniControlEnabled -= 1, 50),
            ModAnimation.AaOpacity(this, Config.Preference.Theme.WindowOpacity / 1000d + 0.4d, 250, 100),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, -TransformPos.Y, 600,
                100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                -TransformRotate.Angle, 500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() =>
            {
                RenderTransform = null;
                IsWindowLoadFinished = true;
                ModBase.Log(
                    $"[System] DPI：{ModBase.DPI}，系统版本：{Environment.OSVersion.VersionString}，PCL 位置：{ModBase.ExePathWithName}");
            }, After: true)
        }, "Form Show");
        // Timer 启动
        ModAnimation.AniStart();
        ModMain.TimerMainStart();
        // 特殊版本提示
        ModBase.RunInNewThread(() =>
        {
            // 特殊版本提示
            try
            {


#if DEBUG || DEBUGCI

                if (Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT") is null)
                {

#if DEBUG
                    const string hint = """
                                        当前运行的 PCL 社区版为 Debug 版本。
                                        该版本仅适合开发者调试运行，可能会有严重的性能下降以及各种奇怪的网络问题。

                                        非开发者用户使用该版本造成的一切问题均不被社区支持，相关 issue 可能会被直接关闭。
                                        除非您是开发者，否则请立即删除该版本，并下载最新稳定版使用。
                                        """;
#else
                    const string hint = """
                                        当前运行的 PCL 社区版为 CI 自动构建版本。
                                        该版本包含最新的漏洞修复、优化和新特性，但性能和稳定性较差，不适合日常使用和制作整合包。
                
                                        除非社区开发者要求或您自己想要这么做，否则请下载最新稳定版使用。
                                        """;
#endif

                    ModMain.MyMsgBox(
                        $"{hint}{"\r\n"}{"\r\n"}可以添加 PCL_DISABLE_DEBUG_HINT 环境变量 (任意值) 来隐藏这个提示。",
                        "特殊版本提示", "我清楚我在做什么", "打开最新版下载页并退出", IsWarn: true, Button2Action: () =>
                        {
                            ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/latest");
                            EndProgram(false);
                        });
                }


#endif
                // EULA 提示
                if (!States.System.LauncherEula)
                    switch (ModMain.MyMsgBox("在使用 PCL 前，请同意 PCL 的用户协议与免责声明。", "协议授权", "同意", "拒绝", "查看用户协议与免责声明",
                                Button3Action: () => ModBase.OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW")))
                    {
                        case 1:
                            {
                                States.System.LauncherEula = true;
                                break;
                            }
                        case 2:
                            {
                                EndProgram(false);
                                break;
                            }
                    }

                // 遥测提示
                if (Config.System.TelemetryConfig.IsDefault())
                {
                    var selection = ModMain.MyMsgBox(
                                "启用遥测数据收集后，启动器将会收集并上报错误与设备环境信息，这可以帮助开发者修复潜在的问题、更好的进行规划和开发。" + "\r\n" +
                                "若启用此功能，我们将会收集以下信息：" + "\r\n" + "\r\n" + "- 启动器内出现的错误" + "\r\n" + "- 启动器版本信息与识别码" +
                                "\r\n" + "- Windows 系统版本与架构" + "\r\n" + "- 已安装的物理内存大小" +
                                "\r\n" + "- NAT 与 IPv6 支持情况" + "\r\n" + "- 是否使用过官方版 PCL、HMCL 或 BakaXL" +
                                "\r\n" + "\r\n" + "这些数据均不与你关联，我们也绝不会向第三方出售数据。" + "\r\n" +
                                "如果不希望启用遥测，可以选择拒绝。这不会影响其他功能的正常使用，但可能会影响开发者修复潜在 Bug。" + "\r\n" + "你可以随时在启动器设置中调整这项设置。",
                                "启用遥测数据收集", "同意", "拒绝");
                    Config.System.TelemetryConfig.SetValue(selection == 1, forceNewValue: true);
                }
                // 启动加载器池
                try
                {
                    ModDownload.DlClientListMojangLoader.Start(1); // PCL 会同时根据这里的加载结果决定是否使用官方源进行下载
                    RunCountSub();
                    UpdateManager.ServerLoader.Start(1);
                    ModBase.RunInNewThread(ModMain.TryClearTaskTemp, "TryClearTaskTemp", ThreadPriority.BelowNormal);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "初始化加载池运行失败", ModBase.LogLevel.Feedback);
                }

                SystemInfo.GetSystemInfo();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "初始弹窗提示运行失败", ModBase.LogLevel.Feedback);
            }
        }, "Start Loader", ThreadPriority.BelowNormal);

        ModBase.Log($"[Start] 第三阶段加载用时：{TimeUtils.GetTimeTick() - ModBase.ApplicationStartTick} ms");
    }

    // 根据打开次数触发的事件
    private void RunCountSub()
    {
        States.System.StartupCount += 1;
    }

    // 升级与降级事件
    private void UpgradeSub(int LastVersionCode)
    {
        ModBase.Log("[Start] 版本号从 " + LastVersionCode + " 升高到 " + ModBase.VersionCode);
        States.System.LastVersion = ModBase.VersionCode;
        // 检查有记录的最高版本号
        int LowerVersionCode;
#if BETA
        LowerVersionCode = States.System.LastBetaVersion;
        if (LowerVersionCode < ModBase.VersionCode)
        {
            States.System.LastBetaVersion = ModBase.VersionCode;
            ModBase.Log($"[Start] 最高版本号从 {LowerVersionCode} 升高到 {ModBase.VersionCode}");
        }
#else
        LowerVersionCode = States.System.LastAlphaVersion;
        if (LowerVersionCode < ModBase.VersionCode)
        {
            States.System.LastAlphaVersion = ModBase.VersionCode;
            ModBase.Log($"[Start] 最高版本号从 {LowerVersionCode} 升高到 {ModBase.VersionCode}");
        }
#endif

        // 被移除的窗口设置选项
        if ((int)Config.Launch.GameWindowMode == 5)
            Config.Launch.GameWindowMode = GameWindowSizeMode.Default;

        // 移动自定义皮肤
        if (LastVersionCode <= 161 && File.Exists(ModBase.ExePath + @"PCL\CustomSkin.png") &&
            !File.Exists(ModBase.PathAppdata + "CustomSkin.png"))
        {
            ModBase.CopyFile(ModBase.ExePath + @"PCL\CustomSkin.png", ModBase.PathAppdata + "CustomSkin.png");
            ModBase.Log("[Start] 已移动离线自定义皮肤 (162)");
        }

        if (LastVersionCode <= 263 && File.Exists(Path.Combine(ModBase.PathTemp, "CustomSkin.png")) &&
            !File.Exists(Path.Combine(ModBase.PathAppdata, "CustomSkin.png")))
        {
            ModBase.CopyFile(Path.Combine(ModBase.PathTemp, "CustomSkin.png"), Path.Combine(ModBase.PathAppdata, "CustomSkin.png"));
            ModBase.Log("[Start] 已移动离线自定义皮肤 (264)");
        }

        // 解除帮助页面的隐藏
        if (LastVersionCode <= 205)
        {
            Config.Preference.Hide.SetupAbout = false;
            ModBase.Log("[Start] 已解除帮助页面的隐藏");
        }

        // 迁移旧版用户档案
        if (LastVersionCode <= 368) ModBase.RunInNewThread(() => ModProfile.MigrateOldProfile());
        // Mod 命名设置迁移
        if (!ModBase.Setup.IsUnset("ToolDownloadTranslate") && ModBase.Setup.IsUnset("ToolDownloadTranslateV2"))
        {
            Config.Download.Comp.NameFormatV2 += 1;
            ModBase.Log("[Start] 已从老版本迁移 Mod 命名设置");
        }

        // 更新后展示社区版提示
        UpdateManager.ShowCEAnnounce();
        // 输出更新日志
        if (LastVersionCode <= 0)
            return;
        if (LowerVersionCode >= ModBase.VersionCode)
            return;
        ShowUpdateLog();
    }

    private void DowngradeSub(int LastVersionCode)
    {
        ModBase.Log("[Start] 版本号从 " + LastVersionCode + " 降低到 " + ModBase.VersionCode);
        States.System.LastVersion = ModBase.VersionCode;
    }

    #endregion

    #region 自定义窗口

    private bool CanResize = true;

    // 重写窗口边缘判定以使 DWM 自带的 resizer 行为看起来比较正常
    private nint _SizeWndProc(nint hWnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // 窗口活动常量
        const int WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        // WPF 尺寸的 offset
        const int offsetWpf = 6;
        const int hitWidthWpf = 5;

        // 过滤非 WM_NCHITTEST 事件
        if (msg != WM_NCHITTEST)
            return nint.Zero;

        // 提取鼠标坐标
        // 没妈的 VB 强转还得检查一下幻想的妈是不是还活着
        var mouseBytes = BitConverter.GetBytes(lParam.ToInt64());
        var xMouse = BitConverter.ToInt16(mouseBytes, 0);
        var yMouse = BitConverter.ToInt16(mouseBytes, 2);

        // 获取窗口参数
        var windowRect = WindowInterop.GetWindowRectangle(hWnd);
        var windowBounds = windowRect.ToWindowBounds();

        // 判断鼠标是否在窗口范围内
        var isInWindow = xMouse >= windowRect.Left && xMouse <= windowRect.Right && yMouse >= windowRect.Top &&
                         yMouse <= windowRect.Bottom;

        // 过滤不在窗口内的请求
        if (!isInWindow)
            return nint.Zero;

        // 如果 CanResize 为 False，直接返回 HTCLIENT
        if (!CanResize)
            return new nint(HTCLIENT);

        // 真实像素尺寸的 offset
        var dpi = VisualTreeHelper.GetDpi(this);
        var offsetPxX = offsetWpf * dpi.DpiScaleX;
        var offsetPxY = offsetWpf * dpi.DpiScaleY;
        var hitWidthPxX = hitWidthWpf * dpi.DpiScaleX;
        var hitWidthPxY = hitWidthWpf * dpi.DpiScaleY;

        // 计算鼠标相对于窗口左上角的物理像素位置
        var relX = xMouse - windowRect.Left;
        var relY = yMouse - windowRect.Top;
        var w = windowBounds.Width;
        var h = windowBounds.Height;

        // 判定是否命中偏移后的热区
        var inLeft = relX >= offsetPxX && relX <= offsetPxX + hitWidthPxX;
        var inRight = relX <= w - offsetPxX && relX >= w - offsetPxX - hitWidthPxX;
        var inTop = relY >= offsetPxY && relY <= offsetPxY + hitWidthPxY;
        var inBottom = relY <= h - offsetPxY && relY >= h - offsetPxY - hitWidthPxY;

        handled = true; // 接管该区域的消息

        // 返回结果
        if (inTop && inLeft)
            return new nint(HTTOPLEFT);
        if (inTop && inRight)
            return new nint(HTTOPRIGHT);
        if (inBottom && inLeft)
            return new nint(HTBOTTOMLEFT);
        if (inBottom && inRight)
            return new nint(HTBOTTOMRIGHT);
        if (inLeft)
            return new nint(HTLEFT);
        if (inRight)
            return new nint(HTRIGHT);
        if (inTop)
            return new nint(HTTOP);
        if (inBottom)
            return new nint(HTBOTTOM);

        // 如果在 0-offset 范围内，返回 HTCLIENT 杀掉默认缩放
        return new nint(HTCLIENT);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        // 硬件加速
        if (Config.System.DisableHardwareAcceleration)
        {
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource is not null) hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
        }

        base.OnSourceInitialized(e);

        // 获取当前窗口句柄
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        if (source is not null)
        {
            // 渲染层允许 Alpha 通道通过
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            // 魔改窗口边缘判定
            source.AddHook(_SizeWndProc);
        }

        // 设置 DWM 窗口框架
        try
        {
            WindowInterop.ExtendFrameIntoClientArea(hwnd, -1);
        }
        catch (Exception ex)
        {
            LogWrapper.Error("DWM 窗口框架应用失败: " + ex.Message);
        }
    }

    // 关闭
    private void FormMain_Closing(object sender, CancelEventArgs e)
    {
        EndProgram(true);
        e.Cancel = true;
    }

    /// <summary>
    ///     正常关闭程序。程序将在执行此方法后约 0.3s 退出。
    /// </summary>
    /// <param name="SendWarning">是否在还有下载任务未完成时发出警告。</param>
    /// <param name="isUpdating">是否正在更新重启</param>
    public void EndProgram(bool SendWarning, bool isUpdating = false)
    {
        // 发出警告
        if (SendWarning && ModNet.HasDownloadingTask())
        {
            if (ModMain.MyMsgBox("还有下载任务尚未完成，是否确定退出？", "提示", "确定", "取消") == 1)
                // 强行结束下载任务
                ModBase.RunInNewThread(() =>
                {
                    ModBase.Log("[System] 正在强行停止任务");
                    foreach (var Task in ModLoader.LoaderTaskbar.ToList())
                        Task.Abort();
                }, "强行停止下载任务");
            else
                return;
        }

        // 关闭联机大厅
        // Await LobbyController.CloseAsync().ConfigureAwait(False)
        // 存储上次使用的档案编号
        ModProfile.SaveProfile();
        // 关闭
        ModBase.RunInUiWait(() =>
        {
            // 清理视频背景
            VideoBack.Stop();
            VideoBack.Source = null;
            VideoBack.Close();
            IsHitTestVisible = false;
            if (RenderTransform is null)
            {
                var TransformPos = new TranslateTransform(0d, 0d);
                var TransformRotate = new RotateTransform(0d);
                var TransformScale = new ScaleTransform(1d, 1d);
                TransformScale.CenterX = Width / 2d;
                TransformScale.CenterY = Height / 2d;
                RenderTransform = new TransformGroup
                    { Children = new TransformCollection([TransformRotate, TransformPos, TransformScale]) };
                ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaOpacity(this, -Opacity, 140, 40,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i =>
                    {
                        TransformScale.ScaleX += (double)i;
                        TransformScale.ScaleY += (double)i;
                    }, 0.88d - TransformScale.ScaleX, 180),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        20d - TransformPos.Y, 180, 0,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        0.6d - TransformRotate.Angle, 180, 0,
                        new ModAnimation.AniEaseInoutFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaCode(() =>
                    {
                        IsHitTestVisible = false;
                        Visibility = Visibility.Collapsed;
                        ShowInTaskbar = false;
                    }, 210),
                    ModAnimation.AaCode(() => EndProgramForce(force: false, isUpdating: isUpdating), 230)
                }, "Form Close");
            }
            else
            {
                EndProgramForce(force: false, isUpdating: isUpdating);
            }

            ModBase.Log("[System] 收到关闭指令");
        });
    }

    private static bool IsLogShown;

    public static void EndProgramForce(ModBase.ProcessReturnValues ReturnCode = ModBase.ProcessReturnValues.Success,
        bool force = true, bool isUpdating = false)
    {
        // On Error Resume Next
        // 关闭联机大厅
        // Await LobbyController.CloseAsync().ConfigureAwait(False)
        ModBase.IsProgramEnded = true;
        ModAnimation.AniControlEnabled += 1;
        if (UpdateManager.IsUpdateWaitingRestart && !isUpdating)
            UpdateManager.UpdateRestart(false, false);
        if (ReturnCode == ModBase.ProcessReturnValues.Exception)
        {
            if (!IsLogShown)
            {
                ModBase.FeedbackInfo();
                ModBase.Log("请在 https://github.com/PCL-Community/PCL2-CE/issues 提交错误报告，以便于社区解决此问题！（这也有可能是原版 PCL 的问题）");
                IsLogShown = true;
                ModBase.ShellOnly(LogWrapper.CurrentLogger.CurrentLogFiles.Last());
            }

            Thread.Sleep(500); // 防止 PCL 在记事本打开前就被掐掉
        }

        ModBase.Log("[System] 程序已退出，返回值：" + ModBase.GetStringFromEnum(ReturnCode));
        // If ReturnCode <> ProcessReturnValues.Success Then Environment.Exit(ReturnCode)
        // Process.GetCurrentProcess.Kill()
        Lifecycle.Shutdown((int)ReturnCode, force);
    }

    private void BtnTitleClose_Click(object sender, EventArgs e)
    {
        EndProgram(true);
    }

    // 移动
    private void FormDragMove(object sender, MouseButtonEventArgs e)
    {
        // On Error Resume Next
        if (((Grid)sender).IsMouseDirectlyOver)
            DragMove();
    }

    // 改变大小
    /// <summary>
    ///     是否可以向注册表储存尺寸改变信息。以此避免初始化时误储存。
    /// </summary>
    public bool IsSizeSaveable;

    private void FormMain_SizeChanged(object? sender = null, EventArgs? e = null)
    {
        if (IsSizeSaveable)
        {
            States.UI.WindowHeight = Height;
            States.UI.WindowWidth = Width;
        }

        if (PanBack is not null)
        {
            RectForm.Rect = new Rect(0d, 0d, PanBack.ActualWidth, PanBack.ActualHeight);

            var formWidth = PanBack.ActualWidth + 0.001d;
            var formHeight = PanBack.ActualHeight + 0.001d;

            PanForm.Width = formWidth;
            PanForm.Height = formHeight;
            PanMain.Width = formWidth;

            if (PanTitle is not null)
                PanMain.Height = Math.Max(0d, formHeight - PanTitle.ActualHeight);
            else
                PanMain.Height = formHeight;

            VideoBack.Width = formWidth;
            VideoBack.Height = formHeight;
        }

        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal; // 修复 #1938
    }

    // 标题栏改变大小
    private void PanTitle_SizeChanged(object sender, EventArgs e)
    {
        if (PanTitleMain.ColumnDefinitions[0].ActualWidth - 30 <= 0)
            PanTitleLeft.ColumnDefinitions[0].MaxWidth = 0;
        else
            PanTitleLeft.ColumnDefinitions[0].MaxWidth = PanTitleMain.ColumnDefinitions[0].ActualWidth - 30;
    }

    // 最小化
    private void BtnTitleMin_Click(object sender, EventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    //“帮助”
    private void BtnTitleHelp_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://www.bilibili.com/video/BV1uT4y1P7CX");
    }

    #endregion

    #region 窗体事件

    public void AddResizer()
    {
        CanResize = true;
    }

    public void RemoveResizer()
    {
        CanResize = false;
    }

    // 按键事件
    private void FormMain_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat)
            return;
        // 调用弹窗：回车选择第一个，Esc 选择最后一个
        if (PanMsg.Children.Count > 0)
        {
            if (e.Key == Key.Enter)
            {
                var msg = PanMsg.Children[0];
                Action? enterAction = msg switch
                {
                    MyMsgInput input => () => input.Btn1_Click(sender, null),
                    MyMsgSelect select => () => select.Btn1_Click(sender, null),
                    MyMsgText text => () => text.Btn1_Click(sender, null),
                    MyMsgMarkdown markdown => () => markdown.Btn1_Click(sender, null),
                    MyMsgLogin login => () => login.Btn1_Click(sender, null),
                    _ => null
                };
                enterAction?.Invoke();
                return;
            }

            if (e.Key == Key.Escape)
            {
                var msg = PanMsg.Children[0];
                Action? escapeAction = msg switch
                {
                    MyMsgInput input => input.Btn2.Visibility == Visibility.Visible
                        ? () => input.Btn2_Click(sender, null)
                        : () => input.Btn1_Click(sender, null),
                    MyMsgSelect select => select.Btn2.Visibility == Visibility.Visible
                        ? () => select.Btn2_Click(sender, null)
                        : () => select.Btn1_Click(sender, null),
                    MyMsgText text => text.Btn3.Visibility == Visibility.Visible
                        ? () => text.Btn3_Click(sender, null)
                        : text.Btn2.Visibility == Visibility.Visible
                            ? () => text.Btn2_Click(sender, null)
                            : () => text.Btn1_Click(sender, null),
                    MyMsgMarkdown markdown => markdown.Btn3.Visibility == Visibility.Visible
                        ? () => markdown.Btn3_Click(sender, null)
                        : markdown.Btn2.Visibility == Visibility.Visible
                            ? () => markdown.Btn2_Click(sender, null)
                            : () => markdown.Btn1_Click(sender, null),
                    MyMsgLogin login => login.Btn3.Visibility == Visibility.Visible
                        ? () => login.Btn3_Click(sender, null)
                        : () => login.Btn1_Click(sender, null),
                    _ => null
                };
                escapeAction?.Invoke();
                return;
            }
        }

        // 按 ESC 返回上一级
        if (e.Key == Key.Escape)
            TriggerPageBack();
        // 更改隐藏实例可见性
        if (e.Key == Key.F11 && PageCurrent == PageType.InstanceSelect)
        {
            ModMain.FrmSelectRight.ShowHidden = !ModMain.FrmSelectRight.ShowHidden;
            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
            return;
        }

        // 更改功能隐藏可见性
        if (e.Key == Key.F12)
        {
            PageSetupUI.HiddenForceShow = !PageSetupUI.HiddenForceShow;
            if (PageSetupUI.HiddenForceShow)
                ModMain.Hint("功能隐藏设置已暂时关闭！", ModMain.HintType.Finish);
            else
                ModMain.Hint("功能隐藏设置已重新开启！", ModMain.HintType.Finish);
            PageSetupUI.HiddenRefresh();
            return;
        }

        // 按 F5 刷新页面
        if (e.Key == Key.F5)
        {
            if (PageLeft is IRefreshable)
                ((IRefreshable)PageLeft).Refresh();
            if (PageRight is IRefreshable)
                ((IRefreshable)PageRight).Refresh();
            return;
        }

        // 调用启动游戏
        if (e.Key == Key.Enter && PageCurrent == PageType.Launch)
        {
            if (ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup)
                ModMain.Hint("木大！");
            else
                ModMain.FrmLaunchLeft.LaunchButtonClick();
        }

        // 修复按下 Alt 后误认为弹出系统菜单导致的冻结
        if (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt)
            e.Handled = true;
    }

    private void FormMain_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 鼠标侧键返回上一级
        if (ModMain.FrmMain!.PanMsg.Children.Count > 0 || ModMain.WaitingMyMsgBox.Any())
            return; // 弹窗中（#5513）
        if (e.ChangedButton == MouseButton.XButton1 || e.ChangedButton == MouseButton.XButton2)
            TriggerPageBack();
    }

    private void TriggerPageBack()
    {
        if (PageCurrent == PageType.Download && PageCurrentSub == PageSubType.DownloadInstall &&
            ModMain.FrmDownloadInstall.IsInSelectPage)
            ModMain.FrmDownloadInstall.ExitSelectPage();
        else if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionInstall &&
                 ModMain.FrmInstanceInstall.IsInSelectPage)
            ModMain.FrmInstanceInstall.ExitSelectPage();
        else
            PageBack();
    }

    // 切回窗口
    private void FormMain_Activated(object sender, EventArgs e)
    {
        try
        {
            if (Config.Download.Comp.ReadClipboard)
                ModComp.CompClipboard.GetClipboardResource();
            if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionMod)
            {
                // Mod 管理自动刷新
                ModMain.FrmInstanceMod.ReloadCompFileList();
            }
            else if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionResourcePack)
            {
                // 资源包管理自动刷新
                if (ModMain.FrmInstanceResourcePack is not null)
                    ModMain.FrmInstanceResourcePack.ReloadCompFileList();
            }
            else if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionShader)
            {
                // 光影包管理自动刷新
                if (ModMain.FrmInstanceShader is not null)
                    ModMain.FrmInstanceShader.ReloadCompFileList();
            }
            else if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionSchematic)
            {
                // 投影原理图管理自动刷新
                if (ModMain.FrmInstanceSchematic is not null)
                    ModMain.FrmInstanceSchematic.ReloadCompFileList();
            }
            else if (PageCurrent == PageType.InstanceSelect)
            {
                // 实例选择自动刷新
                ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                    ModLoader.LoaderFolderRunType.RunOnUpdated, 1, @"versions\");
            }
            else if (ModMain.FrmMain.PageRight is PageInstanceSavesDatapack &&
                     ModMain.FrmInstanceSavesDatapack is not null)
            {
                // 数据包管理自动刷新
                ModMain.FrmInstanceSavesDatapack.ReloadDatapackFileList();
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切回窗口时出错", ModBase.LogLevel.Feedback);
        }
    }

    private IDataObject _HandleDrag_PrevData;
    private DragDropEffects _HandleDrag_PrevEffects;

    // 文件拖放
    private void HandleDrag(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Handled && e.Effects != DragDropEffects.None)
                return;
            // 缓存
            e.Handled = true;
            if (ReferenceEquals(e.Data, _HandleDrag_PrevData))
            {
                e.Effects = _HandleDrag_PrevEffects;
                return;
            }

            // 确定拖放效果
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                var Str = (string)e.Data.GetData(DataFormats.Text);
                if (Str.StartsWithF("authlib-injector:yggdrasil-server:"))
                    e.Effects = DragDropEffects.Copy;
                else if (Str.StartsWithF("file:///")) e.Effects = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var Files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (Files is not null && Files.Length > 0) e.Effects = DragDropEffects.Link;
            }

            _HandleDrag_PrevData = e.Data;
            _HandleDrag_PrevEffects = e.Effects;
            ModBase.Log("[System] 设置拖放类型：" + ModBase.GetStringFromEnum(e.Effects));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "处理拖放时出错", ModBase.LogLevel.Feedback);
        }
    }

    private void FrmMain_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                // 获取文本
                try
                {
                    var Str = (string)e.Data.GetData(DataFormats.Text);
                    ModBase.Log("[System] 接受文本拖拽：" + Str);
                    if (Str.StartsWithF("authlib-injector:yggdrasil-server:"))
                    {
                        // Authlib 拖拽
                        e.Handled = true;
                        e.Effects = DragDropEffects.Copy;
                        var AuthlibServer =
                            WebUtility.UrlDecode(Str.Substring("authlib-injector:yggdrasil-server:".Length));
                        ModBase.Log("[System] Authlib 拖拽：" + AuthlibServer);
                        if (!new HttpValidator().Validate(AuthlibServer).IsValid)
                        {
                            ModMain.Hint($"输入的 Authlib 验证服务器不符合网址格式（{AuthlibServer}）！", ModMain.HintType.Critical);
                            return;
                        }

                        if (ModMain.MyMsgBox($"是否要创建新的第三方验证档案？{"\r\n"}验证服务器地址：{AuthlibServer}", "创建新的第三方验证档案",
                                "确定", "取消") == 2)
                            return;
                        ModProfile.SelectedProfile = null;
                        ModBase.RunInUi(() =>
                        {
                            PageLoginAuth.DraggedAuthServer = AuthlibServer;
                            ModMain.FrmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Auth);
                        });
                        if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionSetup)
                            // 正在服务器选项页，需要刷新设置项显示
                            ModMain.FrmInstanceSetup.Reload();
                    }
                    else if (Str.StartsWithF("file:///"))
                    {
                        // 文件拖拽（例如从浏览器下载窗口拖入）
                        var FilePath = WebUtility.UrlDecode(Str).Substring("file:///".Length).Replace("/", @"\");
                        e.Handled = true;
                        e.Effects = DragDropEffects.Copy;
                        FileDrag(new List<string> { FilePath });
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "无法接取文本拖拽事件", ModBase.LogLevel.Developer);
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 获取文件并检查
                var FilePathRaw = e.Data.GetData(DataFormats.FileDrop);
                if (FilePathRaw is null) // #2690
                {
                    ModMain.Hint("请将文件解压后再拖入！", ModMain.HintType.Critical);
                    return;
                }

                e.Handled = true;
                e.Effects = DragDropEffects.Link;
                FileDrag((IEnumerable<string>)FilePathRaw);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "接取拖拽事件失败", ModBase.LogLevel.Feedback);
        }
    }

    private void FileDrag(IEnumerable<string> FilePathList)
    {
        ModBase.RunInNewThread(() =>
        {
            var FilePath = FilePathList.First();
            ModBase.Log("[System] 接受文件拖拽：" + FilePath + (FilePathList.Any() ? $" 等 {FilePathList.Count()} 个文件" : ""),
                ModBase.LogLevel.Developer);
            // 基础检查
            if (Directory.Exists(FilePathList.First()) && !File.Exists(FilePathList.First()))
            {
                ModMain.Hint("请拖入一个文件，而非文件夹！", ModMain.HintType.Critical);
                return;
            }

            if (!File.Exists(FilePathList.First()))
            {
                ModMain.Hint("拖入的文件不存在：" + FilePathList.First(), ModMain.HintType.Critical);
                return;
            }

            // 多文件拖拽
            if (FilePathList.Count() > 1)
            {
                // 检查是否为同类型文件
                var FirstExtension = FilePathList.First().AfterLast(".").ToLower();
                var AllSameType = FilePathList.All(f => (f.AfterLast(".").ToLower() ?? "") == (FirstExtension ?? ""));

                if (AllSameType &&
                    new[] { "jar", "litemod", "disabled", "old", "litematic", "nbt", "schematic", "schem" }.Contains(
                        FirstExtension))
                {
                }
                // 允许同类型的 Mod 文件或投影文件批量拖拽
                else
                {
                    ModMain.Hint("一次请只拖入相同类型的文件！", ModMain.HintType.Critical);
                    return;
                }
            }

            // 主页
            var Extension = FilePath.AfterLast(".").ToLower();
            if (Extension == "xaml")
            {
                ModBase.Log("[System] 文件后缀为 XAML，作为主页加载");
                if (File.Exists(ModBase.ExePath + @"PCL\Custom.xaml"))
                    if (ModMain.MyMsgBox("已存在一个主页文件，是否要将它覆盖？", "覆盖确认", "覆盖", "取消") == 2)
                        return;

                ModBase.CopyFile(FilePath, ModBase.ExePath + @"PCL\Custom.xaml");
                ModBase.RunInUi(() =>
                {
                    Config.Preference.Homepage.Type = 1;
                    ModMain.FrmLaunchRight.ForceRefresh();
                    ModMain.Hint("已加载主页自定义文件！", ModMain.HintType.Finish);
                });
                return;
            }

            // 安装 Mod
            if (PageInstanceCompResource.InstallMods(FilePathList))
                return;
            // 安装投影文件
            if (new[] { "litematic", "nbt", "schematic", "schem" }.Contains(Extension))
            {
                ModBase.Log($"[System] 文件为 {Extension} 格式，尝试作为原理图安装");
                // 获取当前文件夹路径（如果在资源管理页面）
                string targetFolderPath = null;
                if (PageCurrent == PageType.InstanceSetup && PageCurrentSub == PageSubType.VersionSchematic &&
                    ModMain.FrmInstanceSchematic is not null &&
                    ModMain.FrmInstanceSchematic is PageInstanceCompResource)
                    targetFolderPath = ModMain.FrmInstanceSchematic.CurrentFolderPath;
                PageInstanceCompResource.InstallCompFiles(FilePathList, ModComp.CompType.Schematic, targetFolderPath);
                return;
            }

            // 处理资源安装
            if (PageCurrent == PageType.InstanceSetup && new[] { "zip" }.Any(i => (i ?? "") == (Extension ?? "")))
                switch (PageCurrentSub)
                {
                    case PageSubType.VersionWorld:
                    {
                        var DestFolder = PageInstanceLeft.Instance.PathIndie + @"saves\" +
                                         ModBase.GetFileNameWithoutExtentionFromPath(FilePath);
                        if (Directory.Exists(DestFolder))
                        {
                            ModMain.Hint("发现同名文件夹，无法粘贴：" + DestFolder, ModMain.HintType.Critical);
                            return;
                        }

                        ModBase.ExtractFile(FilePath, DestFolder);
                        ModMain.Hint($"已导入 {ModBase.GetFileNameWithoutExtentionFromPath(FilePath)}",
                            ModMain.HintType.Finish);
                        if (ModMain.FrmInstanceSaves is not null)
                            ModBase.RunInUi(() => ModMain.FrmInstanceSaves.Reload());
                        return;
                    }
                    case PageSubType.VersionResourcePack:
                    {
                        var DestFile = PageInstanceLeft.Instance.PathIndie + @"resourcepacks\" +
                                       ModBase.GetFileNameFromPath(FilePath);
                        if (File.Exists(DestFile))
                        {
                            ModMain.Hint("已存在同名文件：" + DestFile, ModMain.HintType.Critical);
                            return;
                        }

                        ModBase.CopyFile(FilePath, DestFile);
                        ModMain.Hint($"已导入 {ModBase.GetFileNameFromPath(FilePath)}", ModMain.HintType.Finish);
                        if (ModMain.FrmInstanceResourcePack is not null)
                            ModBase.RunInUi(() => ModMain.FrmInstanceResourcePack.ReloadCompFileList());
                        return;
                    }
                    case PageSubType.VersionShader:
                    {
                        var DestFile = PageInstanceLeft.Instance.PathIndie + @"shaderpacks\" +
                                       ModBase.GetFileNameFromPath(FilePath);
                        if (File.Exists(DestFile))
                        {
                            ModMain.Hint("已存在同名文件：" + DestFile, ModMain.HintType.Critical);
                            return;
                        }

                        ModBase.CopyFile(FilePath, DestFile);
                        ModMain.Hint($"已导入 {ModBase.GetFileNameFromPath(FilePath)}", ModMain.HintType.Finish);
                        if (ModMain.FrmInstanceShader is not null)
                            ModBase.RunInUi(() => ModMain.FrmInstanceShader.ReloadCompFileList());
                        return;
                    }
                }

            // 处理投影文件
            if (PageCurrent == PageType.InstanceSetup &&
                new[] { "litematic", "nbt", "schematic", "schem" }.Contains(Extension) &&
                PageCurrentSub == PageSubType.VersionSchematic)
            {
                var DestFile = PageInstanceLeft.Instance.PathIndie + @"schematics\" +
                               ModBase.GetFileNameFromPath(FilePath);
                if (File.Exists(DestFile))
                {
                    ModMain.Hint("已存在同名文件：" + DestFile, ModMain.HintType.Critical);
                    return;
                }

                Directory.CreateDirectory(PageInstanceLeft.Instance.PathIndie + @"schematics\");
                ModBase.CopyFile(FilePath, DestFile);
                ModMain.Hint($"已导入 {ModBase.GetFileNameFromPath(FilePath)}", ModMain.HintType.Finish);
                if (ModMain.FrmInstanceSchematic is not null)
                    ModBase.RunInUi(() => ModMain.FrmInstanceSchematic.ReloadCompFileList());
                return;
            }

            // 安装整合包
            if (new[] { "zip", "rar", "mrpack" }.Any(t =>
                    (t ?? "") == (Extension ?? ""))) // 部分压缩包是 zip 格式但后缀为 rar，总之试一试
            {
                ModBase.Log("[System] 文件为压缩包，尝试作为整合包安装");
                try
                {
                    ModModpack.ModpackInstall(FilePath);
                    return;
                }
                catch (ModBase.CancelledException ex)
                {
                    return; // 用户主动取消
                }
                catch (Exception ex)
                {
                    // 安装失败，继续往后尝试
                }
            }

            if (new[] { "zip", "rar" }.Any(t => (t ?? "") == (Extension ?? "")))
            {
                ModBase.Log("[System] 文件为压缩包，尝试作为存档分析");
                try
                {
                    ModWorld.ReadWorld(FilePath);
                    return;
                }
                catch (ModBase.CancelledException ex)
                {
                    return; // 是存档，但是损坏了
                }
                catch (Exception ex)
                {
                    // 不是存档（或遇到了其他问题），继续往后尝试
                }
            }

            // 错误报告分析
            do
            {
                try
                {
                    ModBase.Log("[System] 尝试进行错误报告分析");
                    var Analyzer = new CrashAnalyzer(ModBase.GetUuid());
                    Analyzer.Import(FilePath);
                    if (!Analyzer.Prepare())
                        break;
                    Analyzer.Analyze();
                    Analyzer.Output(true, new List<string>());
                    return;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "自主错误报告分析失败", ModBase.LogLevel.Feedback);
                }
            } while (false);

            // 未知操作
            ModMain.Hint("PCL 无法确定应当执行的文件拖拽操作……");
        }, "文件拖拽");
    }

    // 接受到 Windows 窗体事件
    public bool IsSystemTimeChanged;

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 30)
        {
            var NowDate = DateTime.Now;
            if (NowDate.Date == ModBase.ApplicationOpenTime.Date)
            {
                ModBase.Log("[System] 系统时间微调为：" + NowDate.ToLongDateString() + " " + NowDate.ToLongTimeString());
                IsSystemTimeChanged = false;
            }
            else
            {
                ModBase.Log("[System] 系统时间修改为：" + NowDate.ToLongDateString() + " " + NowDate.ToLongTimeString());
                IsSystemTimeChanged = true;
            }
        }
        else if (msg == 400 * 16 + 2)
        {
            ModBase.Log("[System] 收到置顶信息：" + hwnd.ToInt64());
            if (!IsWindowLoadFinished)
            {
                ModBase.Log("[System] 窗口尚未加载完成，忽略置顶请求");
                return nint.Zero;
            }

            ShowWindowToTop();
            handled = true;
        }
        else if (msg == 26) // WM_SETTINGCHANGE
        {
            if (Marshal.PtrToStringAuto(lParam) == "ImmersiveColorSet")
            {
                ModBase.Log($"[System] 系统主题更改，深色模式：{SystemTheme.IsSystemInDarkMode()}");
                if (Config.Preference.Theme.ColorMode == ColorMode.System &
                    (ThemeManager.IsDarkMode != SystemTheme.IsSystemInDarkMode())) ThemeService.RefreshColorMode();
            }
        }

        return nint.Zero;
    }

    // 窗口隐藏与置顶
    private bool _Hidden;

    public bool Hidden
    {
        get => _Hidden;
        set
        {
            if (_Hidden == value)
                return;
            _Hidden = value;
            if (value)
            {
                // 隐藏
                Left -= 10000d;
                ShowInTaskbar = false;
                Visibility = Visibility.Hidden;
                ModBase.Log("[System] 窗口已隐藏，位置：(" + Left + "," + Top + ")");
            }
            else
            {
                // 取消隐藏
                if (Left < -2000)
                    Left += 10000d;
                ShowWindowToTop();
            }
        }
    }

    // 解决龙猫的非通用实现史山
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (Hidden)
            Hidden = false;
    }

    /// <summary>
    ///     把当前窗口拖到最前面。
    /// </summary>
    public void ShowWindowToTop()
    {
        ModBase.RunInUi(() =>
        {
            // 这一坨乱七八糟的，别改，改了指不定就炸了，自己电脑还复现不出来
            Visibility = Visibility.Visible;
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Hidden = false;
            Topmost = true; // 偶尔 SetForegroundWindow 失效
            Topmost = false;
            ModMain.SetForegroundWindow(ModBase.FrmHandle);
            Focus();
            ModBase.Log($"[System] 窗口已置顶，位置：({Left}, {Top}), {Width} x {Height}");
        });
    }

    // 背景视频循环播放
    private void VideoEnded(object sender, RoutedEventArgs e)
    {
        VideoBack.Position = TimeSpan.Zero;
        VideoBack.Play();
    }

    // 最小化时暂停背景视频
    private void WindowStateChanged(object sender, EventArgs e)
    {
        switch (WindowState)
        {
            case WindowState.Minimized:
            {
                ModVideoBack.IsMinimized = true;
                ModVideoBack.VideoPause();
                break;
            }
            case WindowState.Normal:
            {
                ModVideoBack.IsMinimized = false;
                ModVideoBack.VideoPlay();
                break;
            }
        }
    }

    #endregion

    #region 切换页面

    // 页面种类与属性
    // 注意，这一枚举在 “切换页面” EventType 中调用，应视作公开 API 的一部分
    /// <summary>
    ///     页面种类。
    /// </summary>
    public enum PageType
    {
        /// <summary>
        ///     启动。
        /// </summary>
        Launch = 0,

        /// <summary>
        ///     下载。
        /// </summary>
        Download = 1,

        /// <summary>
        ///     联机。
        /// </summary>
        Tools = 3,

        /// <summary>
        ///     设置。
        /// </summary>
        Setup = 2,

        /// <summary>
        ///     实例选择。这是一个副页面。
        /// </summary>
        InstanceSelect = 5,

        /// <summary>
        ///     任务管理。这是一个副页面。
        /// </summary>
        TaskManager = 6,

        /// <summary>
        ///     实例设置。这是一个副页面。
        /// </summary>
        InstanceSetup = 7,

        /// <summary>
        ///     资源工程详情。这是一个副页面。
        /// </summary>
        CompDetail = 8,

        /// <summary>
        ///     帮助详情。这是一个副页面。
        /// </summary>
        HelpDetail = 9,

        /// <summary>
        ///     游戏实时日志。这是一个副页面。
        /// </summary>
        GameLog = 10,

        /// <summary>
        ///     存档详细管理，这是一个副页面。
        /// </summary>
        VersionSaves = 12,

        /// <summary>
        ///     主页市场，这是一个副页面。
        /// </summary>
        HomePageMarket = 13
    }

    /// <summary>
    ///     次要页面种类。其数值必须与 StackPanel 中的下标一致。
    /// </summary>
    public enum PageSubType
    {
        Default = 0,
        DownloadInstall = 1,
        DownloadMod = 2,
        DownloadPack = 3,
        DownloadDataPack = 4,
        DownloadResourcePack = 5,
        DownloadShader = 6,
        DownloadWorld = 7,
        DownloadCompFavorites = 8,
        DownloadClient = 9,
        DownloadOptiFine = 10,
        DownloadForge = 11,
        DownloadNeoForge = 12,
        DownloadCleanroom = 13,
        DownloadFabric = 14,
        DownloadQuilt = 15,
        DownloadLiteLoader = 16,
        DownloadLabyMod = 17,
        DownloadLegacyFabric = 18,

        SetupLaunch = 0,
        SetupUI = 1,
        SetupGameManage = 2,
        SetupLink = 3,
        SetupAbout = 4,
        SetupLog = 5,
        SetupFeedback = 6,
        SetupGameLink = 7,
        SetupUpdate = 8,
        SetupJava = 9,
        SetupLauncherMisc = 10,
        SetupLauncherLanguage = 11,

        ToolsGameLink = 1,
        ToolsLauncherHelp = 2,
        ToolsTest = 3,

        VersionOverall = 0,
        VersionSetup = 1,
        VersionExport = 2,
        VersionWorld = 3,
        VersionScreenshot = 4,
        VersionMod = 5,
        VersionModDisabled = 6,
        VersionResourcePack = 7,
        VersionShader = 8,
        VersionSchematic = 9,
        VersionInstall = 10,
        VersionServer = 11,
        VersionSavesInfo = 0,
        VersionSavesBackup = 1,
        VersionSavesDatapack = 2
    }

    /// <summary>
    ///     获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    /// </summary>
    private string PageNameGet(PageStackData Stack)
    {
        switch (Stack.Page)
        {
            case PageType.InstanceSelect:
            {
                return "实例选择";
            }
            case PageType.TaskManager:
            {
                return "任务管理";
            }
            case PageType.GameLog:
            {
                return "实时日志";
            }
            case PageType.InstanceSetup:
            {
                return $"实例设置 - {(PageInstanceLeft.Instance is null ? "未知实例" : PageInstanceLeft.Instance.Name)}";
            }
            case PageType.CompDetail:
            {
                return $"资源下载 - {Stack.Additional.Value.CompProject.TranslatedName}";
            }
            case PageType.HelpDetail:
            {
                return Stack.Additional.Value.HelpEntry.Title;
            }
            case PageType.VersionSaves:
            {
                return $"存档管理 - {ModBase.GetFolderNameFromPath(Stack.Additional.Value.SavePath)}";
            }
            case PageType.HomePageMarket:
            {
                return "主页市场";
            }

            default:
            {
                return "";
            }
        }
    }

    /// <summary>
    ///     刷新次级页面的名称。
    /// </summary>
    public void PageNameRefresh(PageStackData Type)
    {
        LabTitleInner.Text = PageNameGet(Type);
    }

    /// <summary>
    ///     刷新次级页面的名称。
    /// </summary>
    public void PageNameRefresh()
    {
        PageNameRefresh(PageCurrent);
    }

    // 页面状态存储
    /// <summary>
    ///     当前的主页面。
    /// </summary>
    public PageStackData PageCurrent = PageType.Launch;

    /// <summary>
    ///     上一个主页面。
    /// </summary>
    public PageStackData PageLast = PageType.Launch;

    /// <summary>
    ///     当前的子页面。
    /// </summary>
    public PageSubType PageCurrentSub
    {
        get
        {
            switch (PageCurrent.Page)
            {
                case PageType.Download:
                {
                    if (ModMain.FrmDownloadLeft is null)
                        ModMain.FrmDownloadLeft = new PageDownloadLeft();
                    return ModMain.FrmDownloadLeft.PageID;
                }

                case PageType.Setup:
                {
                    if (ModMain.FrmSetupLeft is null)
                        ModMain.FrmSetupLeft = new PageSetupLeft();
                    return ModMain.FrmSetupLeft.PageID;
                }

                case PageType.InstanceSetup:
                {
                    if (ModMain.FrmInstanceLeft is null)
                        ModMain.FrmInstanceLeft = new PageInstanceLeft();
                    return ModMain.FrmInstanceLeft.PageID;
                }

                default:
                {
                    return 0; // 没有子页面
                }
            }
        }
    }

    /// <summary>
    ///     上层页面的编号堆栈，用于返回。
    /// </summary>
    public List<PageStackData> PageStack = new();

    public class PageStackData
    {
        /// <summary>
        /// <list type="bullet">
        ///   <item><description>CompDetail: (CompProject, ExpandedTitles, TargetVersion, TargetLoader, ResourceType)</description></item>
        ///   <item><description>HelpDetail: (HelpEntry, HelpPage)</description></item>
        ///   <item><description>VersionSaves: SavePath</description></item>
        /// </list>
        /// </summary>
        public (
            ModComp.CompProject CompProject,
            List<string> ExpandedTitles,
            string TargetVersion,
            ModComp.CompLoaderType TargetLoader,
            ModComp.CompType ResourceType,
            ModMain.HelpEntry HelpEntry,
            FrameworkElement HelpPage,
            string SavePath
        )? Additional;

        public PageType Page;

        public override bool Equals(object other)
        {
            if (other is null)
                return false;
            if (other is PageStackData)
            {
                var PageOther = (PageStackData)other;
                if (Page != PageOther.Page)
                    return false;
                if (Additional is null) return PageOther.Additional is null;

                return PageOther.Additional is not null && Additional.Equals(PageOther.Additional);
            }

            if (other is int o)
            {
                if ((int)Page == o)
                    return false;
                return Additional is null;
            }

            return false;
        }

        public static bool operator ==(PageStackData left, PageStackData right)
        {
            return EqualityComparer<PageStackData>.Default.Equals(left, right);
        }

        public static bool operator !=(PageStackData left, PageStackData right)
        {
            return !(left == right);
        }

        public static implicit operator PageStackData(PageType Value)
        {
            return new PageStackData { Page = Value };
        }

        public static implicit operator PageType(PageStackData Value)
        {
            return Value.Page;
        }
    }

    public MyPageLeft PageLeft;
    public MyPageRight PageRight;

    // 引发实际页面切换的入口
    private bool IsChangingPage;

    /// <summary>
    ///     切换页面，并引起对应选择 UI 的改变。
    /// </summary>
    public void PageChange(PageStackData Stack, PageSubType SubType = PageSubType.Default)
    {
        if (string.IsNullOrEmpty(PageNameGet(Stack)))
        {
            // 切换到主页面
            PageChangeExit();
            IsChangingPage = true; // 防止下面的勾选直接触发了 PageChangeActual
            ((MyRadioButton)PanTitleSelect.Children[(int)Stack.Page]).SetChecked(true, true,
                string.IsNullOrEmpty(PageNameGet(PageCurrent)));
            IsChangingPage = false;
            switch (Stack.Page)
            {
                case PageType.Download:
                {
                    if (ModMain.FrmDownloadLeft is null)
                        ModMain.FrmDownloadLeft = new PageDownloadLeft();
                    foreach (var item in ModMain.FrmDownloadLeft.PanItem.Children)
                        if (item is MyListItem listItem &&
                            ModBase.Val(listItem.tag) == (double)SubType)
                        {
                            listItem.SetChecked(true, true, Stack == PageCurrent);
                            break;
                        }

                    break;
                }
                case PageType.Setup:
                {
                    if (ModMain.FrmSetupLeft is null)
                        ModMain.FrmSetupLeft = new PageSetupLeft();
                    if (ModMain.FrmSetupLeft.PanItem.Children[(int)SubType] is MyListItem)
                        ((MyListItem)ModMain.FrmSetupLeft.PanItem.Children[(int)SubType]).SetChecked(true, true,
                            Stack == PageCurrent);
                    break;
                }
            }

            PageChangeActual(Stack, SubType);
        }
        else
        {
            // 切换到次页面
            switch (Stack.Page)
            {
                case PageType.InstanceSetup:
                {
                    if (ModMain.FrmInstanceLeft is null)
                        ModMain.FrmInstanceLeft = new PageInstanceLeft();
                    foreach (var item in ModMain.FrmInstanceLeft.PanItem.Children)
                        if (item is MyListItem listItem &&
                            ModBase.Val(listItem.tag) == (double)SubType)
                        {
                            listItem.SetChecked(true, true, Stack == PageCurrent);
                            break;
                        }

                    break;
                }
                case PageType.VersionSaves:
                {
                    if (ModMain.FrmInstanceSavesLeft is null)
                        ModMain.FrmInstanceSavesLeft = new PageInstanceSavesLeft();
                    foreach (var item in ModMain.FrmInstanceSavesLeft.PanItem.Children)
                        if (item is MyListItem listItem &&
                            ModBase.Val(listItem.tag) == (double)SubType)
                        {
                            listItem.SetChecked(true, true, Stack == PageCurrent);
                            break;
                        }

                    break;
                }
            }

            PageChangeActual(Stack, SubType);
        }
    }

    /// <summary>
    ///     通过点击导航栏改变页面。
    /// </summary>
    private void BtnTitleSelect_Click(MyRadioButton sender, bool raiseByMouse)
    {
        if (IsChangingPage)
            return;
        var pageType = (PageType)int.Parse(sender.Tag.ToString());
        PageChangeActual(pageType, PageSubType.Default);
        }

    private void BtnTitleInner_Click(object sender, EventArgs e)
    {
        PageBack();
    }

    /// <summary>
    ///     通过点击返回按钮或手动触发返回来改变页面。
    /// </summary>
    public void PageBack()
    {
        if (PageStack.Any())
            PageChangeActual(PageStack[0], PageSubType.Default);
        else
            PageChange(PageType.Launch);
    }

    // 实际处理页面切换
    /// <summary>
    ///     切换现有页面的实际方法。
    /// </summary>
    private void PageChangeActual(PageStackData Stack, PageSubType SubType)
    {
        if (PageCurrent == Stack && (PageCurrentSub == SubType || (int)SubType == -1))
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            #region 子页面处理

            var PageName = PageNameGet(Stack);
            if (string.IsNullOrEmpty(PageName))
            {
                // 即将切换到一个顶级页面
                PageChangeExit();
            }
            // 即将切换到一个子页面
            else if (PageStack.Any())
            {
                // 子页面 → 另一个子页面，更新
                ModAnimation.AniStart(
                    new[]
                    {
                    ModAnimation.AaOpacity(LabTitleInner, -LabTitleInner.Opacity, 130),
                    ModAnimation.AaCode(() => LabTitleInner.Text = PageName, After: true),
                    ModAnimation.AaOpacity(LabTitleInner, 1d, 150, 30)
                    }, "FrmMain Titlebar SubLayer");
                if (PageStack.Contains(Stack))
                    // 返回到更上层的子页面
                    while (PageStack.Contains(Stack))
                        PageStack.RemoveAt(0);
                else
                    // 进入更深层的子页面
                    PageStack.Insert(0, PageCurrent);
            }
            else
            {
                // 主页面 → 子页面，进入
                PanTitleInner.Visibility = Visibility.Visible;
                PanTitleMain.IsHitTestVisible = false;
                PanTitleInner.IsHitTestVisible = true;
                PageNameRefresh(Stack);
                ModAnimation.AniStart(
                    new[]
                    {
                    ModAnimation.AaOpacity(PanTitleMain, -PanTitleMain.Opacity, 150),
                    ModAnimation.AaX(PanTitleMain, 12d - PanTitleMain.Margin.Left, 150,
                        Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaOpacity(PanTitleInner, 1d - PanTitleInner.Opacity, 150, 200),
                    ModAnimation.AaX(PanTitleInner, -PanTitleInner.Margin.Left, 350, 200,
                        new ModAnimation.AniEaseOutBack()),
                    ModAnimation.AaCode(() => PanTitleMain.Visibility = Visibility.Collapsed, After: true)
                    }, "FrmMain Titlebar FirstLayer");
                PageStack.Insert(0, PageCurrent);
            }

            #endregion

            #region 实际更改页面框架 UI

            PageLast = PageCurrent;
            PageCurrent = Stack;
            switch (Stack.Page)
            {
                case PageType.Launch: // 启动
                    {
                        PageChangeAnim(ModMain.FrmLaunchLeft, ModMain.FrmLaunchRight);
                        break;
                    }
                case PageType.Download: // 下载
                    {
                        ModMain.FrmDownloadLeft ??= new PageDownloadLeft();
                        SubType = ModMain.FrmDownloadLeft.PageID;
                        // PageGet 方法会在未设置 SubType 时指定默认值，并建立相关页面的实例
                        PageChangeAnim(ModMain.FrmDownloadLeft, (FrameworkElement)ModMain.FrmDownloadLeft.PageGet(SubType));
                        break;
                    }
                case PageType.Tools: // 联机
                    {
                        ModMain.FrmToolsLeft ??= new PageToolsLeft();
                        SubType = ModMain.FrmToolsLeft.PageID;
                        PageChangeAnim(ModMain.FrmToolsLeft, (FrameworkElement)ModMain.FrmToolsLeft.PageGet(SubType));
                        break;
                    }
                case PageType.Setup: // 设置
                    {
                        ModMain.FrmSetupLeft ??= new PageSetupLeft();
                        SubType = ModMain.FrmSetupLeft.PageID;
                        PageChangeAnim(ModMain.FrmSetupLeft, (FrameworkElement)ModMain.FrmSetupLeft.PageGet(SubType));
                        break;
                    }
                case PageType.GameLog: // 实时日志
                    {
                        if (ModMain.FrmLogLeft is null)
                            ModMain.FrmLogLeft = new PageLogLeft();
                        if (ModMain.FrmLogLeft is null)
                            ModMain.FrmLogRight = new PageLogRight();
                        PageChangeAnim(ModMain.FrmLogLeft, ModMain.FrmLogRight);
                        break;
                    }
                case PageType.InstanceSelect: // 实例选择
                    {
                        if (ModMain.FrmSelectLeft is null)
                            ModMain.FrmSelectLeft = new PageSelectLeft();
                        if (ModMain.FrmSelectRight is null)
                            ModMain.FrmSelectRight = new PageSelectRight();
                        PageChangeAnim(ModMain.FrmSelectLeft, ModMain.FrmSelectRight);
                        break;
                    }
                case PageType.TaskManager: // 任务管理
                    {
                        if (ModMain.FrmSpeedLeft is null)
                            ModMain.FrmSpeedLeft = new PageSpeedLeft();
                        if (ModMain.FrmSpeedRight is null)
                            ModMain.FrmSpeedRight = new PageSpeedRight();
                        PageChangeAnim(ModMain.FrmSpeedLeft, ModMain.FrmSpeedRight);
                        break;
                    }
                case PageType.InstanceSetup: // 实例设置
                    {
                        ModMain.FrmInstanceLeft ??= new PageInstanceLeft();
                        SubType = ModMain.FrmInstanceLeft.PageID;
                        PageChangeAnim(ModMain.FrmInstanceLeft, (FrameworkElement)ModMain.FrmInstanceLeft.PageGet(SubType));
                        break;
                    }
                case PageType.CompDetail: // Mod 信息
                    {
                        if (ModMain.FrmDownloadCompDetail is null)
                            ModMain.FrmDownloadCompDetail = new PageDownloadCompDetail();
                        PageChangeAnim(new MyPageLeft(), ModMain.FrmDownloadCompDetail);
                        break;
                    }
                case PageType.HelpDetail: // 帮助详情
                    {
                        PageChangeAnim(new MyPageLeft(), Stack.Additional.Value.HelpPage);
                        break;
                    }
                case PageType.VersionSaves: // 存档管理
                    {
                        if (ModMain.FrmInstanceSavesLeft is null)
                            ModMain.FrmInstanceSavesLeft = new PageInstanceSavesLeft();
                        PageInstanceSavesLeft.CurrentSave = Stack.Additional.Value.SavePath;
                        PageChangeAnim(ModMain.FrmInstanceSavesLeft,
                            (FrameworkElement)ModMain.FrmInstanceSavesLeft.PageGet(SubType));
                        break;
                    }
                case PageType.HomePageMarket: // 主页市场
                    {
                        ModMain.FrmHomePageMarket = ModMain.FrmHomePageMarket ?? new PageHomePageMarket();
                        PageChangeAnim(new MyPageLeft(), ModMain.FrmHomePageMarket);
                        break;
                    }
            }

            #endregion

            #region 设置为最新状态

            BtnExtraDownload.ShowRefresh();
            BtnExtraApril.ShowRefresh();

            #endregion

            ModBase.Log("[Control] 切换主要页面：" + ModBase.GetStringFromEnum(Stack) + ", " + (int)SubType);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换主要页面失败（ID " + (int)PageCurrent.Page + "）", ModBase.LogLevel.Feedback);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private void PageChangeAnim(FrameworkElement TargetLeft, FrameworkElement TargetRight)
    {
        ModAnimation.AniStop("FrmMain LeftChange");
        ModAnimation.AniStop("PageLeft PageChange"); // 停止左边栏变更导致的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        ModAnimation.AniControlEnabled += 1;
        // 清除新页面关联性
        if (!(TargetLeft.Parent == null))
            TargetLeft.SetValue(ContentPresenter.ContentProperty, null);
        if (!(TargetRight == null) && !(TargetRight.Parent == null))
            TargetRight.SetValue(ContentPresenter.ContentProperty, null);
        PageLeft = (MyPageLeft)TargetLeft;
        PageRight = (MyPageRight)TargetRight;
        // 触发页面通用动画
        ((MyPageLeft)PanMainLeft.Child).TriggerHideAnimation();
        ((MyPageRight)PanMainRight.Child).PageOnExit();
        ModAnimation.AniControlEnabled -= 1;
        // 执行动画
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ModAnimation.AniControlEnabled += 1;
                // 把新页面添加进容器
                PanMainLeft.Child = PageLeft;
                PageLeft.Opacity = 0d;
                PanMainLeft.Background = null;
                ModAnimation.AniControlEnabled -= 1;
                ModBase.RunInUi(() => PanMainLeft_Resize(PanMainLeft.ActualWidth), true);
            }, 110),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageLeft.Opacity = 1d;
                PageLeft.TriggerShowAnimation();
            }, 30, true)
        }, "FrmMain PageChangeLeft");
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ModAnimation.AniControlEnabled += 1;
                ((MyPageRight)PanMainRight.Child).PageOnForceExit();
                // 把新页面添加进容器
                PanMainRight.Child = PageRight;
                PageRight.Opacity = 0d;
                PanMainRight.Background = null;
                ModAnimation.AniControlEnabled -= 1;
                ModBase.RunInUi(() => BtnExtraBack.ShowRefresh(), true);
            }, 110),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                PageRight.Opacity = 1d;
                PageRight.PageOnEnter();
            }, 30, true)
        }, "FrmMain PageChangeRight");
    }

    /// <summary>
    ///     退出子界面。
    /// </summary>
    private void PageChangeExit()
    {
        if (PageStack.Any())
        {
            // 子页面 → 主页面，退出
            PanTitleMain.Visibility = Visibility.Visible;
            PanTitleMain.IsHitTestVisible = true;
            PanTitleInner.IsHitTestVisible = false;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(PanTitleInner, -PanTitleInner.Opacity, 150),
                    ModAnimation.AaX(PanTitleInner, -18 - PanTitleInner.Margin.Left, 150,
                        Ease: new ModAnimation.AniEaseInFluent()),
                    ModAnimation.AaOpacity(PanTitleMain, 1d - PanTitleMain.Opacity, 150, 200),
                    ModAnimation.AaX(PanTitleMain, -PanTitleMain.Margin.Left, 350, 200,
                        new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaCode(() => PanTitleInner.Visibility = Visibility.Collapsed, After: true)
                }, "FrmMain Titlebar FirstLayer");
            PageStack.Clear();
        }
        // 主页面 → 主页面，无事发生
    }

    // 左边栏改变
    private void PanMainLeft_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;
        PanMainLeft_Resize(e.NewSize.Width);
    }

    private void PanMainLeft_Resize(double NewWidth)
    {
        var Delta = NewWidth - RectLeftBackground.Width;
        if (Math.Abs(Delta) > 0.1d && ModAnimation.AniControlEnabled == 0)
        {
            if (PanMain.Opacity < 0.1d)
                PanMainLeft.IsHitTestVisible = false; // 避免左边栏指向背景未能完美覆盖左边栏
            if (NewWidth > 0d)
                // 宽度足够，显示
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaWidth(RectLeftBackground, NewWidth - RectLeftBackground.Width, 180,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                        ModAnimation.AaOpacity(RectLeftShadow, 1d - RectLeftShadow.Opacity, 180),
                        ModAnimation.AaCode(() => PanMainLeft.IsHitTestVisible = true, 150)
                    }, "FrmMain LeftChange", true);
            else
                // 宽度不足，隐藏
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaWidth(RectLeftBackground, -RectLeftBackground.Width, 180,
                            Ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaOpacity(RectLeftShadow, -RectLeftShadow.Opacity, 180),
                        ModAnimation.AaCode(() => PanMainLeft.IsHitTestVisible = true, 150)
                    }, "FrmMain LeftChange", true);
        }
        else
        {
            RectLeftBackground.Width = NewWidth;
            PanMainLeft.IsHitTestVisible = true;
            ModAnimation.AniStop("FrmMain LeftChange");
        }
    }

    #endregion

    #region 控件拖动

    // 在时钟中调用，使得即使鼠标在窗口外松开，也可以释放控件
    public void DragTick()
    {
        if (ModMain.DragControl is null)
            return;
        if (!(Mouse.LeftButton == MouseButtonState.Pressed)) DragStop();
    }

    // 在鼠标移动时调用，以改变 Slider 位置
    public void DragDoing()
    {
        if (ModMain.DragControl is null)
            return;
        if (Mouse.LeftButton == MouseButtonState.Pressed) 
        {
            ModMain.DragControl.DragDoing();
        }
        else
            DragStop();
    }

    private void PanBack_MouseMove(object sender, EventArgs e)
    {
        DragDoing();
    }

    public void DragStop()
    {
        // 存在其他线程调用的可能性，因此需要确保在 UI 线程运行
        ModBase.RunInUi(() =>
        {
            if (ModMain.DragControl is null)
                return;
            var control = ModMain.DragControl;
            ModMain.DragControl = null;
            control.DragStop(); // 控件会在该事件中判断 DragControl，所以得放在后面
        });
    }

    #endregion

    #region 附加按钮

    // 更新重启
    private void BtnExtraUpdateRestart_Click(object sender, MouseButtonEventArgs e)
    {
        UpdateManager.UpdateRestart(true);
    }

    private bool BtnExtraUpdateRestart_ShowCheck()
    {
        return UpdateManager.IsUpdateWaitingRestart;
    }

    // 音乐
    private void BtnExtraMusic_Click(object sender, MouseButtonEventArgs e)
    {
        ModMusic.MusicControlPause();
    }

    private void BtnExtraMusic_RightClick(object sender, MouseButtonEventArgs e)
    {
        ModMusic.MusicControlNext();
    }

    // 任务管理
    private void BtnExtraDownload_Click(object sender, MouseButtonEventArgs e)
    {
        PageChange(PageType.TaskManager);
    }

    private bool BtnExtraDownload_ShowCheck()
    {
        return ModNet.HasDownloadingTask() && !(PageCurrent == PageType.TaskManager);
    }

    // 投降
    public void AprilGiveup()
    {
        if (ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup)
        {
            ModMain.Hint("=D", ModMain.HintType.Finish);
            ModMain.IsAprilGiveup = true;
            ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleX = 1d;
            ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleY = 1d;
            BtnExtraApril.ShowRefresh();
        }
    }

    private void BtnExtraApril_Click(object sender, MouseButtonEventArgs e)
    {
        AprilGiveup();
    }

    public bool BtnExtraApril_ShowCheck()
    {
        return ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup && PageCurrent == PageType.Launch;
    }

    // 关闭 Minecraft
    private void BtnExtraShutdown_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (ModLaunch.McLaunchLoaderReal is not null)
                ModLaunch.McLaunchLoaderReal.Abort();
            foreach (var Watcher in ModWatcher.McWatcherList)
                Watcher.Kill();
            ModMain.Hint("已关闭运行中的 Minecraft！", ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "强制关闭所有 Minecraft 失败", ModBase.LogLevel.Feedback);
        }
    }

    public bool BtnExtraShutdown_ShowCheck()
    {
        return ModWatcher.HasRunningMinecraft;
    }

    // 游戏日志
    private void BtnExtraLog_Click(object sender, MouseButtonEventArgs e)
    {
        PageChange(PageType.GameLog);
    }

    public bool BtnExtraLog_ShowCheck()
    {
        if (ModMain.FrmLogLeft is null || ModMain.FrmLogRight is null || PageCurrent == PageType.GameLog)
            return false;
        return ModMain.FrmLogLeft.ShownLogs.Count > 0;
    }

    /// <summary>
    ///     返回顶部。
    /// </summary>
    public void BackToTop()
    {
        var RealScroll = BtnExtraBack_GetRealChild();
        if (RealScroll is not null)
            RealScroll.PerformVerticalOffsetDelta(-RealScroll.VerticalOffset);
        else
            ModBase.Log("[UI] 无法返回顶部，未找到合适的 RealScroll", ModBase.LogLevel.Hint);
    }

    private void BtnExtraBack_Click(object sender, MouseButtonEventArgs e)
    {
        BackToTop();
    }

    private bool BtnExtraBack_ShowCheck()
    {
        var RealScroll = BtnExtraBack_GetRealChild();
        return RealScroll is not null && RealScroll.Visibility == Visibility.Visible &&
               RealScroll.VerticalOffset > Height + (BtnExtraBack.Show ? 0 : 700);
    }

    private MyScrollViewer? BtnExtraBack_GetRealChild()
    {
        if (PanMainRight.Child is null || !(PanMainRight.Child is MyPageRight))
            return null;
        return ((MyPageRight)PanMainRight.Child).PanScroll;
    }

    #endregion
}
