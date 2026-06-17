using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

public static partial class ModMain
{
    public static FormMain? frmMain;
    public static SplashScreen? frmStart;
    public static PageLaunchLeft? frmLaunchLeft;
    public static PageLaunchRight? frmLaunchRight;
    public static PageLogLeft? frmLogLeft;
    public static PageLogRight? frmLogRight;
    public static PageSelectLeft? frmSelectLeft;
    public static PageSelectRight? frmSelectRight;
    public static PageSpeedLeft? frmSpeedLeft;
    public static PageSpeedRight? frmSpeedRight;
    public static PageToolsLeft? frmToolsLeft;
    public static PageToolsGameLink? frmToolsGameLink;
    public static PageToolsTest? frmToolsTest;
    public static PageDownloadLeft? frmDownloadLeft;
    public static PageDownloadInstall? frmDownloadInstall;
    public static PageDownloadClient? frmDownloadClient;
    public static PageDownloadOptiFine? frmDownloadOptiFine;
    public static PageDownloadLiteLoader? frmDownloadLiteLoader;
    public static PageDownloadForge? frmDownloadForge;
    public static PageDownloadNeoForge? frmDownloadNeoForge;
    public static PageDownloadCleanroom? frmDownloadCleanroom;
    public static PageDownloadFabric? frmDownloadFabric;
    public static PageDownloadQuilt? frmDownloadQuilt;
    public static PageDownloadLabyMod? frmDownloadLabyMod;
    public static PageDownloadLegacyFabric? frmDownloadLegacyFabric;
    public static PageDownloadMod? frmDownloadMod;
    public static PageDownloadPack? frmDownloadPack;
    public static PageDownloadDataPack? frmDownloadDataPack;
    public static PageDownloadShader? frmDownloadShader;
    public static PageDownloadResourcePack? frmDownloadResourcePack;
    public static PageDownloadWorld? frmDownloadWorld;
    public static PageDownloadCompFavorites? frmDownloadCompFavorites;
    public static PageSetupLeft? frmSetupLeft;
    public static PageSetupLaunch? frmSetupLaunch;
    public static PageSetupUI? frmSetupUI;
    public static PageSetupGameManage? frmSetupGameManage;
    public static PageSetupUpdate? frmSetupUpdate;
    public static PageSetupJava? frmSetupJava;
    public static PageSetupAbout? frmSetupAbout;
    public static PageSetupLog? frmSetupLog;
    public static PageSetupFeedback? frmSetupFeedback;
    public static PageSetupGameLink? frmSetupGameLink;
    public static PageSetupLauncherLanguage? frmSetupLauncherLanguage;
    public static PageSetupLauncherMisc? frmSetupLauncherMisc;
    public static PageLoginAuth? frmLoginAuth;
    public static PageLoginMs? frmLoginMs;
    public static PageLoginProfile? frmLoginProfile;
    public static PageLoginProfileSkin? frmLoginProfileSkin;
    public static PageLoginOffline? frmLoginOffline;
    public static PageInstanceLeft? frmInstanceLeft;
    public static PageInstanceOverall? frmInstanceOverall;
    public static PageInstanceCompResource? frmInstanceMod;
    public static PageInstanceModDisabled? frmInstanceModDisabled;
    public static PageInstanceScreenshot? frmInstanceScreenshot;
    public static PageInstanceSaves? frmInstanceSaves;
    public static PageInstanceCompResource? frmInstanceShader;
    public static PageInstanceCompResource? frmInstanceSchematic;
    public static PageInstanceCompResource? frmInstanceResourcePack;
    public static PageInstanceSetup? frmInstanceSetup;
    public static PageInstanceInstall? frmInstanceInstall;
    public static PageInstanceExport? frmInstanceExport;
    public static PageInstanceServer? frmInstanceServer;
    public static PageInstanceSavesLeft? frmInstanceSavesLeft;
    public static PageInstanceSavesInfo? frmInstanceSavesInfo;
    public static PageInstanceSavesDatapack? frmInstanceSavesDatapack;
    public static PageDownloadCompDetail? frmDownloadCompDetail;
    public static PageHomepageNewsView? frmHomepageNews;

    public static MySlider? dragControl = null;
    private static int timer4Count;
    private static int timer150Count;

    /// <summary>
    ///     等待弹出的提示列表。以 {String, HintType, Log As Boolean} 形式存储为数组。
    /// </summary>
    private static ModBase.SafeList<HintMessage> HintWaiting
    {
        get => field ??= new ModBase.SafeList<HintMessage>();
        set;
    }

    private static void TimerMain()
    {
        try
        {
            #region 每 50ms 执行一次的代码

            HintTick();
            DialogManager.Instance?.Tick();
            frmMain!.DragTick();
            ModLoader.LoaderTaskbarProgressRefresh();
        }

        #endregion

        catch (Exception ex)
        {
            ModBase.Log(ex, "短程主时钟执行异常", ModBase.LogLevel.Critical);
        }

        timer4Count += 1;
        if (timer4Count == 4)
        {
            timer4Count = 0;
            try
            {
                #region 每 250ms 执行一次的代码
            }

            #endregion

            catch (Exception ex)
            {
                ModBase.Log(ex, "中程主时钟执行异常");
            }
        }

        timer150Count += 1;
        if (timer150Count == 150)
        {
            timer150Count = 0;
            try
            {
                #region 每 7.5s 执行一次的代码

                if (frmMain!.BtnExtraApril_ShowCheck() && aprilDistance != 0)
                    frmMain.BtnExtraApril.Ribble();
                // 以未知原因窗口被丢到一边去的修复（Top、Left = -25600），还有 #745
                ModBase.RunInUi(() =>
                {
                    if (!frmMain.Hidden)
                    {
                        if (frmMain.Top < -9000) frmMain.Top = 100d;
                        if (frmMain.Left < -9000) frmMain.Left = 100d;
                    }
                }); // 窗口拉至最大时 Left = -18.8
            }

            #endregion

            catch (Exception ex)
            {
                ModBase.Log(ex, "长程主时钟执行异常", ModBase.LogLevel.Critical);
            }
        }
    }

    public static void TimerMainStart()
    {
        ModBase.RunInNewThread(() =>
        {
            try
            {
                while (true)
                {
                    ModBase.RunInUiWait(TimerMain);
                    Thread.Sleep((int)Math.Round(50d * 0.98d));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "程序主时钟出错", ModBase.LogLevel.Feedback);
            }
        }, "Timer Main");
        if (!isAprilEnabled)
            return;
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var lastTime = Environment.TickCount;
                while (true)
                {
                    if (lastTime != Environment.TickCount)
                    {
                        lastTime = Environment.TickCount;
                        ModBase.RunInUiWait(TimerFool);
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "愚人节主时钟出错", ModBase.LogLevel.Feedback);
            }
        }, "Timer Main Fool");
    }

    #region 弹出提示

    /// <summary>
    ///     提示信息的种类。
    /// </summary>
    public enum HintType
    {
        /// <summary>
        ///     信息，通常是蓝色的“i”。
        /// </summary>
        /// <remarks></remarks>
        Info,

        /// <summary>
        ///     已完成，通常是绿色的“√”。
        /// </summary>
        /// <remarks></remarks>
        Finish,

        /// <summary>
        ///     错误，通常是红色的“×”。
        /// </summary>
        /// <remarks></remarks>
        Critical
    }

    private struct HintMessage
    {
        public string Text;
        public HintType Type;
        public bool Log;
    }


    /// <summary>
    ///     在窗口弹出提示文本。
    /// </summary>
    public static void Hint(string? text, HintType type = HintType.Info, bool log = true)
    {
        var normalized = (text ?? "").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
        if (HintWaiting.Any(h => h.Text == normalized && h.Type == type)) return;
        HintWaiting.Add(new HintMessage { Text = normalized, Type = type, Log = log });
    }

    public static void HintWrapper_OnShow(string message, HintTheme messageTheme)
    {
        var hintType = messageTheme switch
        {
            HintTheme.Error => HintType.Critical,
            HintTheme.Info => HintType.Info,
            _ => HintType.Finish
        };
        Hint(message, hintType);
    }

    private static void HintTick()
    {
        try
        {
            frmMain!.PanHint.HorizontalAlignment = HorizontalAlignment.Right;
            frmMain.PanHint.VerticalAlignment = VerticalAlignment.Bottom;

            // Keep toasts above any visible extra buttons in the same corner
            var extraHeight = frmMain.PanExtraButtons.ActualHeight;
            frmMain.PanHint.Margin = new Thickness(0, 0, 0, extraHeight > 0 ? extraHeight + 20 : 20);

            if (!HintWaiting.Any())
                return;

            var currentHint = HintWaiting[0];

            // If a visible toast already shows this exact message, shake it instead of stacking a new one.
            // This must run before the cap check — a duplicate needs no new slot, so no existing toast should be evicted.
            var duplicate = frmMain.PanHint.Children.OfType<MyToast>()
                .FirstOrDefault(t => !t.IsDismissing && t.Context == currentHint.Text && t.ToastType == currentHint.Type);
            if (duplicate != null)
            {
                duplicate.Emphasize();
                HintWaiting.RemoveAt(0);
                return;
            }

            // Only count toasts that are still visible (not mid-dismiss-animation)
            var activeCount = frmMain.PanHint.Children.OfType<MyToast>().Count(t => !t.IsDismissing);
            if (activeCount >= 5)
            {
                // Dismiss the oldest active toast and wait for it to leave before adding the next one
                var oldest = frmMain.PanHint.Children.OfType<MyToast>().FirstOrDefault(t => !t.IsDismissing);
                oldest?.Dismiss();
                return;
            }

            var toast = new MyToast
            {
                Context = currentHint.Text,
                ToastType = currentHint.Type,
                Icon = currentHint.Type switch
                {
                    HintType.Finish => "lucide/circle-check",
                    HintType.Critical => "lucide/circle-minus",
                    _ => "lucide/info"
                },
                DisplayDuration = (800d + ModBase.MathClamp(currentHint.Text.Length, 5d, 23d) * 180d) * ModAnimation.aniSpeed
            };

            frmMain.PanHint.Children.Add(toast);
            toast.Show();

            if (currentHint.Log)
                ModBase.Log("[UI] 弹出提示：" + currentHint.Text);
            HintWaiting.RemoveAt(0);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "显示弹出提示失败", ModBase.LogLevel.Normal);
        }
    }

    private static void HideAllHint()
    {
        foreach (MyToast toast in frmMain!.PanHint.Children.OfType<MyToast>().ToList())
            toast.Dismiss();
    }

    #endregion

    #region 愚人节

    public static bool isAprilEnabled = DateTime.Now.Month == 4 && DateTime.Now.Day == 1;
    public static bool isAprilGiveup = false;
    private static Vector aprilSpeed = new(0d, 0d);
    private static int aprilIdieCount;
    private static Point aprilMousePosLast = new(0d, 0d);
    private static int aprilDistance;

    private static void TimerFool()
    {
        try
        {
            if (frmLaunchLeft is null || frmLaunchLeft.AprilPosTrans is null || frmMain.lastMouseArg is null)
                return;
            if (isAprilGiveup || frmMain.pageCurrent != FormMain.PageType.Launch ||
                ModAnimation.AniControlEnabled != 0 || !frmLaunchLeft.BtnLaunch.IsLoaded)
                return;

            // 计算是否空闲
            var mousePos = frmMain.lastMouseArg.GetPosition(frmMain);
            if (mousePos == aprilMousePosLast)
            {
                aprilIdieCount += 1;
            }
            else
            {
                aprilMousePosLast = mousePos;
                aprilIdieCount = 0;
            }

            // 计算躲避移动
            Vector direction;
            double distance;
            var buttonWidth = frmLaunchLeft.BtnLaunch.ActualWidth / 2d;
            var buttonHeight = frmLaunchLeft.BtnLaunch.ActualHeight / 2d;
            var vec = (Vector)(frmMain.lastMouseArg.GetPosition(frmLaunchLeft.BtnLaunch) -
                               new Vector(buttonWidth, buttonHeight));
            var dir = new Vector(vec.X, vec.Y);
            dir.Normalize();
            direction = -dir;
            distance = new Vector(Math.Max(0d, Math.Abs(vec.X) - buttonWidth),
                Math.Max(0d, Math.Abs(vec.Y) - buttonHeight)).Length;
            var breathScale = Math.Sin(timer150Count / 37.5d * Math.PI);
            var acc = Math.Max(0d, breathScale * 0.25d - 0.65d - Math.Log((distance + 0.4d) / 200d)) * direction; // 加速度
            // 计算回归移动
            if (aprilIdieCount >= 64 * 5)
            {
                var safeDist = (Vector)(frmMain.lastMouseArg.GetPosition(frmMain.PanMain) -
                                        new Vector(buttonWidth, frmMain.PanMain.ActualHeight - buttonHeight * 3d));
                var back = new Vector(frmLaunchLeft.AprilPosTrans.X, frmLaunchLeft.AprilPosTrans.Y);
                if (safeDist.Length > 250d && back.Length > 0.4d)
                {
                    acc -= back * 0.0005d;
                    back.Normalize();
                    acc -= back * 0.15d;
                }
            }

            // 回到边界
            var relative = frmLaunchLeft.BtnLaunch.TranslatePoint(new Point(0d, 0d), frmMain.PanForm);
            if (relative.X < -buttonWidth * 2d)
            {
                frmLaunchLeft.AprilPosTrans.X += frmMain.PanForm.ActualWidth + buttonWidth * 2d; // 离开左边界
                aprilSpeed.X -= 80d;
                if (relative.Y < 0d)
                    frmLaunchLeft.AprilPosTrans.Y += buttonHeight * 2.5d;
                else if (relative.Y > frmMain.PanForm.ActualHeight - buttonHeight * 2d)
                    frmLaunchLeft.AprilPosTrans.Y -= buttonHeight * 2.5d;
            }
            else if (relative.X > frmMain.PanForm.ActualWidth)
            {
                frmLaunchLeft.AprilPosTrans.X -= frmMain.PanForm.ActualWidth + buttonWidth * 2d; // 离开右边界
                aprilSpeed.X += 80d;
                if (relative.Y < 0d)
                    frmLaunchLeft.AprilPosTrans.Y += buttonHeight * 2.5d;
                else if (relative.Y > frmMain.PanForm.ActualHeight - buttonHeight * 2d)
                    frmLaunchLeft.AprilPosTrans.Y -= buttonHeight * 2.5d;
            }
            else if (relative.Y < -buttonHeight * 2d)
            {
                frmLaunchLeft.AprilPosTrans.Y += frmMain.PanForm.ActualHeight + buttonHeight * 2d; // 离开上边界
                aprilSpeed.Y -= 25d;
                if (relative.X < 0d)
                    frmLaunchLeft.AprilPosTrans.X += buttonWidth * 2d;
                else if (relative.X > frmMain.PanForm.ActualWidth - buttonWidth * 2d)
                    frmLaunchLeft.AprilPosTrans.X -= buttonWidth * 2d;
            }
            else if (relative.Y > frmMain.PanForm.ActualHeight)
            {
                frmLaunchLeft.AprilPosTrans.Y -= frmMain.PanForm.ActualHeight + buttonHeight * 2d; // 离开下边界
                aprilSpeed.Y += 25d;
                if (relative.X < 0d)
                    frmLaunchLeft.AprilPosTrans.X += buttonWidth * 2d;
                else if (relative.X > frmMain.PanForm.ActualWidth - buttonWidth * 2d)
                    frmLaunchLeft.AprilPosTrans.X -= buttonWidth * 2d;
            }

            // 移动
            aprilSpeed = aprilSpeed * 0.8d + acc;
            var speedValue = Math.Min(60d, aprilSpeed.Length);
            if (speedValue < 0.01d)
                return;
            aprilSpeed.Normalize();
            aprilSpeed *= speedValue;
            aprilDistance = (int)Math.Round(aprilDistance + speedValue);
            frmLaunchLeft.AprilPosTrans.X += aprilSpeed.X;
            frmLaunchLeft.AprilPosTrans.Y += aprilSpeed.Y;
            // 大小改变
            frmLaunchLeft.AprilScaleTrans.ScaleX =
                ModBase.MathClamp(1d - (Math.Abs(direction.X) - Math.Abs(direction.Y)) * (speedValue / 160d), 0.2d,
                    1.8d);
            frmLaunchLeft.AprilScaleTrans.ScaleY =
                ModBase.MathClamp(1d - (Math.Abs(direction.Y) - Math.Abs(direction.X)) * (speedValue / 100d), 0.2d,
                    1.8d);
            // 放弃提示
            if (aprilDistance > 4000)
            {
                aprilDistance = -4000;
                switch (RandomUtils.NextInt(0, 3))
                {
                    case 0:
                    {
                        Hint("放弃吧！只需要点一下右下角的小白旗……");
                        break;
                    }
                    case 1:
                    {
                        Hint("看到右下角的那面小白旗了吗？");
                        break;
                    }
                    case 2:
                    {
                        Hint("这里建议点一下右下角的小白旗投降呢.jpg");
                        break;
                    }
                    case 3:
                    {
                        Hint("右下角的小白旗永远等着你……");
                        break;
                    }
                }
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "愚人节移动出错", ModBase.LogLevel.Feedback);
        }
    }

    #endregion

    #region 系统

    /// <summary>
    ///     把某个 PCL 窗口拖到最前面。
    /// </summary>
    public static void ShowWindowToTop(nint handle)
    {
        try
        {
            PostMessage(handle, 400 * 16 + 2, 0L, 0L);
            SetForegroundWindow(handle); // 不在这里放不行，神秘 WinAPI，建议别动
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置窗口置顶失败", ModBase.LogLevel.Hint);
        }
    }

    [DllImport("user32", EntryPoint = "FindWindowA")]
    public static extern nint FindWindow(string className, string windowName);

    [DllImport("user32")]
    public static extern int SetForegroundWindow(nint hWnd);

    [DllImport("user32", EntryPoint = "PostMessageA")]
    private static extern bool PostMessage(nint hWnd, uint msg, long wParam, long lParam);

    /// <summary>
    ///     将特定程序设置为使用高性能显卡启动。
    ///     如果失败，则抛出异常。
    /// </summary>
    public static void SetGPUPreference(string executeable, bool wantHighPerformance = true)
    {
        const string GPU_PERFERENCE_REG_KEY = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string GPU_PERFERENCE_REG_VALUE_HIGH = "GpuPreference=2;";
        const string GPU_PERFERENCE_REG_VALUE_DEFAULT = "GpuPreference=0;";
        // Const GPU_PERFERENCE_REG_VALUE_POWER_SAVING As String = "GpuPreference=1;"

        var isCurrentHighPerformance = false;
        // 查看现有设置
        // 就知道 My.Computer，改个注册表 Microsoft.Win32.Registry 几年前的 API 了不用，还在这 My.Computer 都 5202 年了 My 你大爷
        using (var readOnlyKey = Registry.CurrentUser.OpenSubKey(GPU_PERFERENCE_REG_KEY, false))
        {
            if (readOnlyKey is not null)
            {
                var currentValue = readOnlyKey.GetValue(executeable);
                if (GPU_PERFERENCE_REG_VALUE_HIGH == (currentValue?.ToString() ?? "")) isCurrentHighPerformance = true;
            }
            else
            {
                // 创建父级键
                ModBase.Log("[System] 需要创建显卡设置的父级键");
                Registry.CurrentUser.CreateSubKey(GPU_PERFERENCE_REG_KEY);
            }
        }

        ModBase.Log($"[System] 当前程序 ({executeable}) 的显卡设置为高性能: {isCurrentHighPerformance}");
        if (isCurrentHighPerformance ^ wantHighPerformance)
            // 写入新设置
            using (var writeKey = Registry.CurrentUser.OpenSubKey(GPU_PERFERENCE_REG_KEY, true))
            {
                writeKey.SetValue(executeable,
                    wantHighPerformance ? GPU_PERFERENCE_REG_VALUE_HIGH : GPU_PERFERENCE_REG_VALUE_DEFAULT);
                ModBase.Log($"[System] 已调整程序 ({executeable}) 显卡设置: {wantHighPerformance}");
            }
    }

    /// <summary>
    /// 对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    /// /// </summary>
    public static string ArgumentReplace(string text, Func<string, string> escapeHandler = null, bool replaceTime = true) 
    {
    // 预处理
    if (text is null) return null;
    
    Func<string, string> replacer = (s) =>
    {
        if (s is null) return "";
        if (escapeHandler is null) return s;
        if (s.Contains(":\\")) s = ModBase.ShortenPath(s);
        return escapeHandler(s);
    };
    
    // 基础
    text = text.Replace("{pcl_version}", replacer(ModBase.versionBaseName));
    text = text.Replace("{pcl_version_code}", replacer(ModBase.versionCode.ToString()));
    text = text.Replace("{pcl_version_branch}", replacer(ModBase.versionBranchName));
    text = text.Replace("{pcl_branch}", replacer(ModBase.versionBranchName));
    text = text.Replace("{identify}", replacer(Identify.LauncherId));
    text = text.Replace("{path}", replacer(Basics.ExecutableDirectory));
    text = text.Replace("{path_with_name}", replacer(Basics.ExecutableName));
    text = text.Replace("{path_temp}", replacer(ModBase.pathTemp));
    
    // 时间
    if (replaceTime) // 在窗口标题中，时间会被后续动态替换，所以此时不应该替换
    {
        text = text.Replace("{date}", replacer(Lang.Date(DateTime.Now, "d")));
        text = text.Replace("{time}", replacer(Lang.Date(DateTime.Now, "T")));
    }
    
    // Minecraft
    text = text.Replace("{java}", replacer(ModLaunch.mcLaunchJavaSelected?.Installation.JavaFolder));
    text = text.Replace("{minecraft}", replacer(ModFolder.mcFolderSelected));
    
    if (ModInstanceList.McMcInstanceSelected is not null)
    {
        text = text.Replace("{version_path}", replacer(ModInstanceList.McMcInstanceSelected.PathInstance));
        text = text.Replace("{verpath}", replacer(ModInstanceList.McMcInstanceSelected.PathInstance));
        text = text.Replace("{version_indie}", replacer(ModInstanceList.McMcInstanceSelected.PathIndie));
        text = text.Replace("{verindie}", replacer(ModInstanceList.McMcInstanceSelected.PathIndie));
        text = text.Replace("{name}", replacer(ModInstanceList.McMcInstanceSelected.Name));
        
        if (new[] { "unknown", "old", "pending" }.Contains(ModInstanceList.McMcInstanceSelected.Info.VanillaName))
        {
            text = text.Replace("{version}", replacer(ModInstanceList.McMcInstanceSelected.Name));
        }
        else
        {
            text = text.Replace("{version}", replacer(ModInstanceList.McMcInstanceSelected.Info.VanillaName));
        }
    }
    else
    {
        text = text.Replace("{version_path}", replacer(null));
        text = text.Replace("{verpath}", replacer(null));
        text = text.Replace("{version_indie}", replacer(null));
        text = text.Replace("{verindie}", replacer(null));
        text = text.Replace("{name}", replacer(null));
        text = text.Replace("{version}", replacer(null));
    }
    
    // 验证信息
    if (ModLaunch.mcLoginLoader.State == ModBase.LoadState.Finished)
    {
        text = text.Replace("{user}", replacer(ModLaunch.mcLoginLoader.output.Name));
        text = text.Replace("{uuid}", replacer(ModLaunch.mcLoginLoader.output.Uuid.ToLower()));
        
        switch (ModLaunch.mcLoginLoader.input.LoginType)
        {
            case ModLaunch.McLoginType.Legacy:
                text = text.Replace("{login}", replacer("离线"));
                break;
            case ModLaunch.McLoginType.Ms:
                text = text.Replace("{login}", replacer("正版"));
                break;
            case ModLaunch.McLoginType.Auth:
                text = text.Replace("{login}", replacer("Authlib-Injector"));
                break;
        }
    }
    else
    {
        text = text.Replace("{user}", replacer(null));
        text = text.Replace("{uuid}", replacer(null));
        text = text.Replace("{login}", replacer(null));
    }
    
    // 高级
    text = ModBase.RegexReplaceEach(text, @"\{hint\}", m => replacer(PageToolsTest.GetRandomHint()));
    text = ModBase.RegexReplaceEach(text, @"\{cave\}", m => replacer(PageToolsTest.GetRandomCave()));
    text = ModBase.RegexReplaceEach(text, @"\{setup:([a-zA-Z0-9]+)\}", m =>
    {
        if (ConfigService.TryGetConfigItemNoType(m.Groups[1].Value, out var item) && item.Source != ConfigSource.SharedEncrypt)
            return replacer(item.GetValueNoType(ModInstanceList.McMcInstanceSelected?.PathInstance)?.ToString() ?? "");
        return replacer("");
    });
    text = ModBase.RegexReplaceEach(text, @"\{varible:([^:\}]+)(?::([^\}]+))?\}", m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value, m.Groups[2].Value)));
    text = ModBase.RegexReplaceEach(text, @"\{variable:([^:\}]+)(?::([^\}]+))?\}", m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value, m.Groups[2].Value)));
    
    return text;
}
    #endregion

    #region 任务缓存

    private static bool isTaskTempCleared;
    private static bool isTaskTempClearing;

    /// <summary>
    ///     尝试清理任务缓存文件夹。
    ///     在整次运行中只会实际清理一次。
    /// </summary>
    public static void TryClearTaskTemp()
    {
        if (!isTaskTempCleared)
        {
            isTaskTempCleared = true;
            isTaskTempClearing = true;
            try
            {
                ModBase.Log("[System] 开始清理任务缓存文件夹");
                ModBase.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL", "TaskTemp"));
                ModBase.DeleteDirectory($@"{ModBase.pathTemp}TaskTemp\");
                ModBase.Log("[System] 已清理任务缓存文件夹");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清理任务缓存文件夹失败");
            }
            finally
            {
                isTaskTempClearing = false;
            }
        }
        else if (isTaskTempClearing)
        {
            // 等待另一个清理步骤完成
            while (isTaskTempClearing)
                Thread.Sleep(1);
        }
    }

    /// <summary>
    ///     申请一个可用于任务缓存的临时文件夹，以 \ 结尾。这些文件夹无需进行后续清理。
    ///     若所有缓存位置均没有权限，会抛出异常。
    /// </summary>
    /// <param name="requireNonSpace">是否要求路径不包含空格。</param>
    public static string RequestTaskTempFolder(bool requireNonSpace = false)
    {
        TryClearTaskTemp();
        string resultFolder;
        do
        {
            try
            {
                resultFolder = $@"{ModBase.pathTemp}TaskTemp\{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";
                if (requireNonSpace && resultFolder.Contains(" "))
                    break; // 带空格
                Directory.CreateDirectory(resultFolder);
                ModBase.CheckPermissionWithException(resultFolder);
                return resultFolder;
            }
            catch
            {
            }
        } while (false);

        // 使用备用路径
        resultFolder =
            Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL", "TaskTemp", $"{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}");
        Directory.CreateDirectory(resultFolder);
        ModBase.CheckPermission(resultFolder);
        return resultFolder;
    }

    #endregion
    
    public static void RaiseCustomEvent(DependencyObject control)
    {
        // 收集事件列表
        var events = CustomEventService.GetEvents(control).ToList();
        var eventType = CustomEventService.GetEventType(control);
        if (eventType != CustomEvent.EventType.None)
            events.Add(new CustomEvent(eventType, CustomEventService.GetEventData(control)));

        if (!events.Any()) return;

        ModBase.RunInNewThread(() =>
            {
                foreach (var e in events)
                    e.Raise();
            }, $"执行自定义事件 {ModBase.GetUuid()}");
    }
}
