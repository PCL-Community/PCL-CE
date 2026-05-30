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

public static class ModMain
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
    public static PageToolsHelp? frmToolsHelp;
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
    public static PageHomePageMarket? frmHomePageMarket;
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

    public static ModLoader.LoaderTask<int, List<HelpEntry>> helpLoader = new("Help Page", HelpLoad, null,
        ThreadPriority.BelowNormal);

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

    /// <summary>
    ///     等待显示的弹窗。
    /// </summary>
    public static List<MyMsgBoxConverter> WaitingMyMsgBox { get; } = [];

    private static void TimerMain()
    {
        try
        {
            #region 每 50ms 执行一次的代码

            HintTick();
            MyMsgBoxTick();
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
        public string text;
        public HintType type;
        public bool log;
    }


    /// <summary>
    ///     在窗口左下角弹出提示文本。
    /// </summary>
    public static void Hint(string? Text, HintType Type = HintType.Info, bool Log = true)
    {
        HintWaiting.Add(new HintMessage { text = Text ?? "", type = Type, log = Log });
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
            // Tag 存储了：{ 是否可以重用, Uuid }
            if (!HintWaiting.Any())
                return;
            while (HintWaiting.Any())
            {
                // '清除空提示
                // If IsNothing(HintWaiting(0)) OrElse IsNothing(HintWaiting(0)(0)) Then
                // HintWaiting.RemoveAt(0)
                // Continue Do
                // End If
                var currentHint = HintWaiting[0];
                // 去回车
                currentHint.text = currentHint.text.Replace("\r\n", " ").Replace("\r", " ")
                    .Replace("\n", " ");
                // 超量提示直接忽略
                if (frmMain!.PanHint.Children.Count >= 20)
                    goto EndHint;
                // 检查是否有重复提示
                Border? doubleStack = null;
                foreach (Border stack in frmMain.PanHint.Children)
                    if (stack.Tag is object[] tagArray && (bool)tagArray[0] &&
                                              (((TextBlock)stack.Child).Text ?? "") == (currentHint.text ?? ""))
                        doubleStack = stack;
                // 获取渐变颜色
                ModBase.MyColor targetColor0, targetColor1;
                var percent = 0.3d;
                switch (currentHint.type)
                {
                    case HintType.Info:
                    {
                        targetColor0 = new ModBase.MyColor(215d, 37d, 155d, 252d);
                        targetColor1 = new ModBase.MyColor(215d, 10d, 142d, 252d);
                        break;
                    }
                    case HintType.Finish:
                    {
                        targetColor0 = new ModBase.MyColor(215d, 33d, 177d, 33d);
                        targetColor1 = new ModBase.MyColor(215d, 29d, 160d, 29d); // HintType.Critical
                        break;
                    }

                    default:
                    {
                        targetColor0 = new ModBase.MyColor(215d, 255d, 53d, 11d);
                        targetColor1 = new ModBase.MyColor(215d, 255d, 43d, 0d);
                        break;
                    }
                }

                if (doubleStack is not null)
                {
                    var doubleStackTag = (object[])doubleStack.Tag;
                    // 有重复提示，且该提示的进入动画已播放
                    if (!ModAnimation.AniIsRun($"Hint Show {doubleStackTag[1]}"))
                    {
                        ModAnimation.AniStop($"Hint Hide {doubleStackTag[1]}");
                        var delay = (800d + ModBase.MathClamp(currentHint.text!.Length, 5d, 23d) * 180d) *
                                    ModAnimation.aniSpeed;
                        ModAnimation.AniStart(new[]
                            {
                                ModAnimation.AaX(doubleStack, -12 - doubleStack.Margin.Left, 50,
                                    Ease: new ModAnimation.AniEaseOutFluent()),
                                ModAnimation.AaX(doubleStack, -8, 50, 50, new ModAnimation.AniEaseInFluent()),
                                ModAnimation.AaX(doubleStack, 8d, 50, 100, new ModAnimation.AniEaseOutFluent()),
                                ModAnimation.AaX(doubleStack, -8, 50, 150, new ModAnimation.AniEaseInFluent()),
                                ModAnimation.AaDouble(i =>
                                {
                                    percent += (double)i;
                                    var gradient = (LinearGradientBrush)doubleStack.Background;
                                    gradient.GradientStops[0].Color = targetColor0 * percent +
                                                                      new ModBase.MyColor(255d, 255d, 255d) *
                                                                      (1d - percent);
                                    gradient.GradientStops[1].Color = targetColor1 * percent +
                                                                      new ModBase.MyColor(255d, 255d, 255d) *
                                                                      (1d - percent);
                                }, 0.7d, 250),
                                ModAnimation.AaX(doubleStack, -50, 200, (int)Math.Round(delay),
                                    new ModAnimation.AniEaseInFluent()),
                                ModAnimation.AaOpacity(doubleStack, -1, 150, (int)Math.Round(delay)),
                                ModAnimation.AaCode(() => doubleStackTag[0] = false,
                                    (int)Math.Round(delay)),
                                ModAnimation.AaHeight(doubleStack, -26, 100, Ease: new ModAnimation.AniEaseOutFluent(),
                                    After: true),
                                ModAnimation.AaCode(() => frmMain.PanHint.Children.Remove(doubleStack), After: true)
                            },
                            $"Hint Hide {doubleStackTag[1]}");
                    }
                }
                else
                {
                    // 准备控件
                    var newHintTag = new object[] { true, ModBase.GetUuid() };
                    var newHintControl = new Border
                    {
                        Tag = newHintTag, Margin = new Thickness(-70, 0d, 20d, 0d),
                        Opacity = 0d,
                        Height = 0d, HorizontalAlignment = HorizontalAlignment.Left,
                        CornerRadius = new CornerRadius(0d, 6d, 6d, 0d),
                        Background = new LinearGradientBrush(
                            new GradientStopCollection(new List<GradientStop>
                            {
                                new(targetColor0 * percent + new ModBase.MyColor(255d, 255d, 255d) * (1d - percent),
                                    0d),
                                new(targetColor1 * percent + new ModBase.MyColor(255d, 255d, 255d) * (1d - percent), 1d)
                            }), 90d),
                        Child = new TextBlock
                        {
                            TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13d, Text = currentHint.text,
                            Foreground = new ModBase.MyColor(255d, 255d, 255d), Margin = new Thickness(33d, 5d, 8d, 5d)
                        }
                    };
                    // AddHandler NewHintControl.MouseLeftButtonDown, AddressOf HideAllHint
                    frmMain.PanHint.Children.Add(newHintControl);
                    // 控件动画
                    var animations = new List<ModAnimation.AniData>();
                    if (frmMain.PanHint.Children.Count > 1)
                        // 已有提示
                        animations.Add(ModAnimation.AaHeight(newHintControl, 26d, 150,
                            Ease: new ModAnimation.AniEaseOutFluent()));
                    else
                        // 是唯一提示
                        newHintControl.Height = 26d;
                    // 开始动画
                    animations.AddRange([
                        ModAnimation.AaX(newHintControl, 30d,
                            Ease: new ModAnimation.AniEaseOutElastic(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaX(newHintControl, 20d, 200, Ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaOpacity(newHintControl, 1d, 100),
                        ModAnimation.AaDouble(i =>
                        {
                            percent += (double)i;
                            var gradient = (LinearGradientBrush)newHintControl.Background;
                            gradient.GradientStops[0].Color = targetColor0 * percent +
                                                              new ModBase.MyColor(255d, 255d, 255d) * (1d - percent);
                            gradient.GradientStops[1].Color = targetColor1 * percent +
                                                              new ModBase.MyColor(255d, 255d, 255d) * (1d - percent);
                        }, 0.7d, 250, 100)
                    ]);
                    ModAnimation.AniStart(animations, $"Hint Show {newHintTag[1]}");
                    // 结束动画
                    var delay = (800d + ModBase.MathClamp(currentHint.text!.Length, 5d, 23d) * 180d) *
                                ModAnimation.aniSpeed;
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaX(newHintControl, -50, 200, (int)Math.Round(delay),
                                new ModAnimation.AniEaseInFluent()),
                            ModAnimation.AaOpacity(newHintControl, -1, 150, (int)Math.Round(delay)),
                            ModAnimation.AaCode(() => newHintTag[0] = false, (int)Math.Round(delay)),
                            ModAnimation.AaHeight(newHintControl, -26, 100, Ease: new ModAnimation.AniEaseOutFluent(),
                                After: true),
                            ModAnimation.AaCode(() => frmMain.PanHint.Children.Remove(newHintControl), After: true)
                        }, $"Hint Hide {newHintTag[1]}");
                }

                // 结束处理
                EndHint: ;

                if (currentHint.log)
                    ModBase.Log("[UI] 弹出提示：" + currentHint.text);
                HintWaiting.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "显示弹出提示失败", ModBase.LogLevel.Normal);
        }
    }

    private static void HideAllHint()
    {
        foreach (Border Control in frmMain!.PanHint.Children)
        {
            var controlTag = (object[])Control.Tag;
            Control.IsHitTestVisible = false;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaX(Control, -50, 200, Ease: new ModAnimation.AniEaseInFluent()),
                    ModAnimation.AaOpacity(Control, -1, 150, Ease: new ModAnimation.AniEaseInFluent()),
                    ModAnimation.AaCode(() => controlTag[0] = false),
                    ModAnimation.AaHeight(Control, -26, 100, Ease: new ModAnimation.AniEaseOutFluent(), After: true),
                    ModAnimation.AaCode(() => frmMain.PanHint.Children.Remove(Control), After: true)
                }, $"Hint Hide {controlTag[1]}");
        }
    }

    #endregion

    #region 弹窗

    /// <summary>
    ///     存储弹窗信息的转换器。
    /// </summary>
    public class MyMsgBoxConverter
    {
        // 设置轮询 Url
        public object authUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        public string button1 = "";

        /// <summary>
        ///     点击第一个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action button1Action;

        public string button2 = "";

        /// <summary>
        ///     点击第二个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action button2Action;

        public string button3 = "";

        /// <summary>
        ///     点击第三个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action button3Action;

        /// <summary>
        ///     输入模式：文本框的文本。
        ///     选择模式：需要放进去的 List(Of MyListItem)。
        ///     登录模式：登录步骤 1 中返回的 JSON。
        /// </summary>
        public object content;

        public bool forceWait;

        /// <summary>
        ///     有多个按钮时，是否给第一个按钮加高亮。
        /// </summary>
        public bool highLight;

        /// <summary>
        ///     输入模式：提示文本。
        /// </summary>
        public string hintText = "";

        /// <summary>
        ///     弹窗是否已经关闭。
        /// </summary>
        public bool isExited = false;

        public bool isWarn;

        /// <summary>
        ///     输入模式：输入的文本。若点击了 非 第一个按钮，则为 Nothing。
        ///     选择模式：点击的按钮编号，从 1 开始。
        ///     登录模式：字符串数组 {AccessToken, RefreshToken} 或一个 Exception。
        /// </summary>
        public object result;

        public string text;
        public string title;
        public MyMsgBoxType type;

        /// <summary>
        ///     输入模式：输入验证规则。
        /// </summary>
        public Collection<IValidator<string>> validateRules;

        public DispatcherFrame waitFrame = new(true);
    }

    public enum MyMsgBoxType
    {
        Text,
        Select,
        Input,
        Login,
        Markdown
    }

    private static string GetDefaultDialogTitle() => Lang.Text("Common.Dialog.Title");

    private static string GetDefaultConfirmText() => Lang.Text("Common.Action.Confirm");

    private static string GetDefaultCancelText() => Lang.Text("Common.Action.Cancel");

    /// <summary>
    ///     显示弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    /// <param name="Title">弹窗的标题。</param>
    /// <param name="Caption">弹窗的内容。</param>
    /// <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="Button2">显示的第二个按钮，默认为空。</param>
    /// <param name="Button3">显示的第三个按钮，默认为空。</param>
    /// <param name="Button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="Button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="Button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int MyMsgBox(string Caption, string? Title = null, string? Button1 = null, string? Button2 = "",
        string? Button3 = "", bool IsWarn = false, bool HighLight = true, bool ForceWait = false,
        Action Button1Action = null, Action Button2Action = null, Action Button3Action = null)
    {
        Title ??= GetDefaultDialogTitle();
        Button1 ??= GetDefaultConfirmText();
        Button2 ??= "";
        Button3 ??= "";
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            type = MyMsgBoxType.Text, button1 = Button1, button2 = Button2, button3 = Button3, text = Caption,
            isWarn = IsWarn, title = Title, highLight = HighLight, forceWait = true, button1Action = Button1Action,
            button2Action = Button2Action, button3Action = Button3Action
        };
        WaitingMyMsgBox.Add(converter);
        if (ModBase.RunInUi())
            // 若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick();
        if (Button2.Length > 0 || ForceWait)
        {
            // 若有多个按钮则开始等待
            if (frmMain is null || (frmMain.PanMsg is null && ModBase.RunInUi()))
            {
                // 主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(converter);
                if (Button2.Length > 0)
                {
                    var rawResult = Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)(Button3.Length > 0 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo) +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    switch (rawResult)
                    {
                        case MsgBoxResult.Yes:
                        {
                            converter.result = 1;
                            break;
                        }
                        case MsgBoxResult.No:
                        {
                            converter.result = 2;
                            break;
                        }
                        case MsgBoxResult.Cancel:
                        {
                            converter.result = 3;
                            break;
                        }
                    }
                }
                else
                {
                    Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)MsgBoxStyle.OkOnly +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    converter.result = 1;
                }

                ModBase.Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" + Button1 + "," + Button2 + "," + Button3,
                    ModBase.LogLevel.Debug);
            }
            else
            {
                try
                {
                    frmMain.DragStop();
                    ComponentDispatcher.PushModal();
                    Dispatcher.PushFrame(converter.waitFrame);
                }
                finally
                {
                    ComponentDispatcher.PopModal();
                }
            }

            ModBase.Log($"[Control] 普通弹框返回：{converter.result ?? "null"}");
            return (int)converter.result;
        }

        // 不进行等待，直接返回
        return 1;
    }

    /// <summary>
    ///     显示弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    /// <param name="Title">弹窗的标题。</param>
    /// <param name="Caption">弹窗的内容。</param>
    /// <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="Button2">显示的第二个按钮，默认为空。</param>
    /// <param name="Button3">显示的第三个按钮，默认为空。</param>
    /// <param name="Button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="Button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="Button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int MyMsgBoxMarkdown(string Caption, string? Title = null, string? Button1 = null, string? Button2 = "",
        string? Button3 = "", bool IsWarn = false, bool HighLight = true, bool ForceWait = false,
        Action Button1Action = null, Action Button2Action = null, Action Button3Action = null)
    {
        Title ??= GetDefaultDialogTitle();
        Button1 ??= GetDefaultConfirmText();
        Button2 ??= "";
        Button3 ??= "";
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            type = MyMsgBoxType.Markdown, button1 = Button1, button2 = Button2, button3 = Button3, text = Caption,
            isWarn = IsWarn, title = Title, highLight = HighLight, forceWait = true, button1Action = Button1Action,
            button2Action = Button2Action, button3Action = Button3Action
        };
        WaitingMyMsgBox.Add(converter);
        if (ModBase.RunInUi())
            // 若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick();
        if (Button2.Length > 0 || ForceWait)
        {
            // 若有多个按钮则开始等待
            if (frmMain is null || (frmMain.PanMsg is null && ModBase.RunInUi()))
            {
                // 主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(converter);
                if (Button2.Length > 0)
                {
                    var rawResult = Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)(Button3.Length > 0 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo) +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    switch (rawResult)
                    {
                        case MsgBoxResult.Yes:
                        {
                            converter.result = 1;
                            break;
                        }
                        case MsgBoxResult.No:
                        {
                            converter.result = 2;
                            break;
                        }
                        case MsgBoxResult.Cancel:
                        {
                            converter.result = 3;
                            break;
                        }
                    }
                }
                else
                {
                    Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)MsgBoxStyle.OkOnly +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    converter.result = 1;
                }

                ModBase.Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" + Button1 + "," + Button2 + "," + Button3,
                    ModBase.LogLevel.Debug);
            }
            else
            {
                try
                {
                    frmMain.DragStop();
                    ComponentDispatcher.PushModal();
                    Dispatcher.PushFrame(converter.waitFrame);
                }
                finally
                {
                    ComponentDispatcher.PopModal();
                }
            }

            ModBase.Log($"[Control] 普通弹框返回：{converter.result ?? "null"}");
            return (int)converter.result;
        }

        // 不进行等待，直接返回
        return 1;
    }

    /// <summary>
    ///     显示输入框并返回输入的文本。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="Title">弹窗的标题。</param>
    /// <param name="ValidateRules">文本框的输入检测。</param>
    /// <param name="Text">弹窗的介绍文本。</param>
    /// <param name="DefaultInput">文本框的默认内容。</param>
    /// <param name="HintText">文本框的提示内容。</param>
    /// <param name="Button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="Button2">显示的第二个按钮，默认为“取消”。</param>
    /// <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static string MyMsgBoxInput(string Title, string Text = "", string DefaultInput = "",
        Collection<IValidator<string>>? ValidateRules = null, string HintText = "", string? Button1 = null,
        string? Button2 = null, bool IsWarn = false)
    {
        Button1 ??= GetDefaultConfirmText();
        Button2 ??= GetDefaultCancelText();
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            text = Text, hintText = HintText, type = MyMsgBoxType.Input,
            validateRules = ValidateRules ?? [], button1 = Button1, button2 = Button2,
            content = DefaultInput, isWarn = IsWarn, title = Title
        };
        WaitingMyMsgBox.Add(converter);
        // 虽然我也不知道这是啥但是能用就成了 :)
        try
        {
            frmMain?.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(converter.waitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        ModBase.Log($"[Control] 输入弹框返回：{converter.result}");
        return converter.result?.ToString();
    }

    /// <summary>
    ///     显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="Title">弹窗的标题。</param>
    /// <param name="Button1">显示的第一个按钮，默认为 “确定”。</param>
    /// <param name="Button2">显示的第二个按钮，默认为空。</param>
    /// <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int? MyMsgBoxSelect(List<IMyRadio> Selections, string? Title = null, string? Button1 = null,
        string? Button2 = "", bool IsWarn = false)
    {
        Title ??= GetDefaultDialogTitle();
        Button1 ??= GetDefaultConfirmText();
        Button2 ??= "";
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            type = MyMsgBoxType.Select, button1 = Button1, button2 = Button2, content = Selections, isWarn = IsWarn,
            title = Title
        };
        WaitingMyMsgBox.Add(converter);
        // 虽然我也不知道这是啥但是能用就成了 :)
        try
        {
            if (frmMain is not null)
                frmMain.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(converter.waitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        ModBase.Log($"[Control] 选择弹框返回：{converter.result ?? "null"}");
        return (int?)converter.result;
    }


    public static void MyMsgBoxTick()
    {
        try
        {
            if (frmMain is null || frmMain.PanMsg is null || frmMain.WindowState == WindowState.Minimized)
                return;
            if (frmMain.PanMsg.Children.Count > 0)
            {
                // 弹窗中
                frmMain.PanMsgBackground.Visibility = Visibility.Visible;
            }
            else if (WaitingMyMsgBox.Any())
            {
                // 没有弹窗，显示一个等待的弹窗
                frmMain.PanMsgBackground.Visibility = Visibility.Visible;
                switch (WaitingMyMsgBox[0].type)
                {
                    case MyMsgBoxType.Input:
                    {
                        frmMain.PanMsg.Children.Add(new MyMsgInput(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Select:
                    {
                        frmMain.PanMsg.Children.Add(new MyMsgSelect(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Text:
                    {
                        frmMain.PanMsg.Children.Add(new MyMsgText(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Login:
                    {
                        frmMain.PanMsg.Children.Add(new MyMsgLogin(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Markdown:
                    {
                        frmMain.PanMsg.Children.Add(new MyMsgMarkdown(WaitingMyMsgBox[0]));
                        break;
                    }
                }

                WaitingMyMsgBox.RemoveAt(0);
            }
            // 没有弹窗，没有等待的弹窗
            else if (!(frmMain.PanMsgBackground.Visibility == Visibility.Collapsed))
            {
                frmMain.PanMsgBackground.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "处理等待中的弹窗失败", ModBase.LogLevel.Feedback);
        }
    }

    public static void MsgBoxWrapper_OnShow(string message, string caption, ICollection<MsgBoxButtonInfo> buttons,
        MsgBoxTheme theme, bool block, ref int result)
    {
        var btnText1 = buttons.Count < 1 ? GetDefaultConfirmText() : buttons.ElementAt(0).Context;
        var btnAct1 = (Action)(buttons.Count < 1 ? (object)null : buttons.ElementAt(0).OnClick);
        var btnText2 = buttons.Count < 2 ? GetDefaultCancelText() : buttons.ElementAt(1).Context;
        var btnAct2 = (Action)(buttons.Count < 2 ? (object)null : buttons.ElementAt(1).OnClick);
        var btnText3 = buttons.Count < 3 ? "" : buttons.ElementAt(2).Context;
        var btnAct3 = (Action)(buttons.Count < 3 ? (object)null : buttons.ElementAt(2).OnClick);

        var isWarn = theme == MsgBoxTheme.Warning || theme == MsgBoxTheme.Error;

        result = MyMsgBox(message, caption, btnText1, btnText2, btnText3, isWarn, ForceWait: block,
            Button1Action: btnAct1, Button2Action: btnAct2, Button3Action: btnAct3);
    }

    #endregion

    #region 页面声明

    // 在最后进行页面声明，避免颜色尚未加载完毕

    // 窗体声明


    // 页面声明（出于单元测试考虑，初始化页面已转入 FormMain 中）


    // 工具页面声明


    // 下载页面声明


    // 设置页面声明


    // 登录页面声明


    // 实例设置页面声明


    // 实例存档页面


    // 资源信息分页声明
    
    #endregion

    #region 帮助

    public class HelpEntry
    {
        /// <summary>
        ///     显示描述。
        /// </summary>
        public string desc;

        public string eventData;
        public string eventType;

        // 动作

        /// <summary>
        ///     是否为 “执行事件”。
        /// </summary>
        public bool isEvent;

        // 显示（可选）

        /// <summary>
        ///     帮助项的自定义图标。可能为 Nothing。
        /// </summary>
        public string logo;

        /// <summary>
        ///     原始信息路径。用于刷新。
        /// </summary>
        public string rawPath;

        /// <summary>
        ///     检索关键字。
        /// </summary>
        public string search;

        /// <summary>
        ///     是否在公开版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        /// </summary>
        public bool showInPublic = true;

        /// <summary>
        ///     是否显示在搜索结果。默认为 True。
        /// </summary>
        public bool showInSearch = true;

        /// <summary>
        ///     是否在快照版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        /// </summary>
        public bool showInSnapshot = true;

        // 基础

        /// <summary>
        ///     显示标题。
        /// </summary>
        public string title;

        /// <summary>
        ///     用于分类的标签列表。
        /// </summary>
        public List<string> types;

        /// <summary>
        ///     若非执行事件，其对应的 .xaml 本地文件内容。
        /// </summary>
        public string xamlContent;

        // 转换

        /// <summary>
        ///     从文件初始化 HelpEntry 对象，失败会抛出异常。
        /// </summary>
        public HelpEntry(string FilePath)
        {
            rawPath = FilePath;
            var jsonData = (JsonObject)ModBase.GetJson(ModMain.ArgumentReplace(ModBase.ReadFile(FilePath)));
            if (jsonData is null)
                throw new FileNotFoundException("未找到帮助文件：" + FilePath, FilePath);
            // 加载常规信息
            if (jsonData["Title"] is not null)
                title = (string)jsonData["Title"];
            else
                throw new ArgumentException("未找到 Title 项");
            desc = (string)(jsonData["Description"] ?? "");
            search = (string)(jsonData["Keywords"] ?? "");
            logo = (string)jsonData["Logo"]; // 为保持 Nothing，不要加 If
            showInSearch = (bool)(jsonData["ShowInSearch"] ?? showInSearch);
            showInPublic = (bool)(jsonData["ShowInPublic"] ?? showInPublic);
            showInSnapshot = (bool)(jsonData["ShowInSnapshot"] ?? showInSnapshot);
            types = new List<string>();
            foreach (var NameOfType in (IEnumerable)(jsonData["Types"] ?? ModBase.GetJson("[]")))
                types.Add(NameOfType.ToString());
            // 加载事件信息
            if ((bool)(jsonData["IsEvent"] ?? false))
            {
                eventType = Enum.Parse(typeof(CustomEvent.EventType), jsonData["EventType"].ToString()).ToString();
                eventData = (jsonData["EventData"] ?? "").ToString();
                isEvent = true;
            }
            else
            {
                var xamlAddress = FilePath.ToLower().Replace(".json", ".xaml");
                if (File.Exists(xamlAddress))
                {
                    xamlContent = ModBase.ReadFile(xamlAddress);
                    isEvent = false;
                }
                else
                {
                    throw new FileNotFoundException("未找到帮助条目 .json 对应的 .xaml 文件（" + xamlAddress + "）");
                }
            }
        }

        /// <summary>
        ///     获取该 HelpEntry 对应的 MyListItem。
        /// </summary>
        public MyListItem ToListItem()
        {
            return SetToListItem(new MyListItem());
        }

        /// <summary>
        ///     将属性设置入一个现有的 ListItem。
        /// </summary>
        public MyListItem SetToListItem(MyListItem Item)
        {
            string logoPath;
            if (isEvent)
            {
                if (eventType == "弹出窗口")
                    logoPath = ModBase.pathImage + "Blocks/GrassPath.png";
                else
                    logoPath = ModBase.pathImage + "Blocks/CommandBlock.png";
            }
            else
            {
                logoPath = ModBase.pathImage + "Blocks/Grass.png";
            }

            // 设置属性
            Item.SnapsToDevicePixels = true;
            Item.Title = title;
            Item.Info = desc;
            Item.Logo = this.logo ?? logoPath;
            Item.Height = 42d;
            Item.Type = MyListItem.CheckType.Clickable;
            Item.Tag = this;
            CustomEventService.SetEventType(Item, CustomEvent.EventType.None); //清空自定义事件属性，它们会被下面的点击事件处理
            CustomEventService.SetEventData(Item, null);
            // 项目的点击事件
            Item.Click += (sender, e) => PageToolsHelp.OnItemClick((HelpEntry)((MyListItem)sender).Tag);
            return Item;
        }
    }


    private static readonly object helpLoadLock = new();

    /// <summary>
    ///     初始化帮助列表对象。
    /// </summary>
    private static void HelpLoad(ModLoader.LoaderTask<int, List<HelpEntry>> Loader)
    {
        lock (helpLoadLock) // 避免重复解压文件导致出错
        {
            try
            {
                // 解压内置文件
                HelpExtract();

                // 遍历文件
                var fileList = new List<string>();
                try
                {
                    var ignoreList = new List<string>();
                    // 读取自定义文件
                    if (Directory.Exists(ModBase.exePath + @"PCL\Help\"))
                        foreach (var File in ModBase.EnumerateFiles(ModBase.exePath + @"PCL\Help\"))
                            switch (File.Extension.ToLower() ?? "")
                            {
                                case ".helpignore":
                                {
                                    // 加载忽略列表
                                    ModBase.Log("[Help] 发现 .helpignore 文件：" + File.FullName);
                                    foreach (var Line in ModBase.ReadFile(File.FullName)
                                                 .Split("\r\n".ToCharArray()))
                                    {
                                        var realString = Line.BeforeFirst("#").Trim();
                                        if (string.IsNullOrWhiteSpace(realString))
                                            continue;
                                        ignoreList.Add(realString);
                                        if (ModBase.modeDebug)
                                            ModBase.Log("[Help]  > " + realString);
                                    }

                                    break;
                                }
                                case ".json":
                                {
                                    fileList.Add(File.FullName);
                                    break;
                                }
                            }

                    ModBase.Log("[Help] 已扫描 PCL 文件夹下的帮助文件，目前总计 " + fileList.Count + " 条");
                    // 读取自带文件
                    foreach (var File in ModBase.EnumerateFiles(ModBase.pathHelpFolder))
                    {
                        // 跳过非 Json 文件与以 . 开头的文件夹
                        if (File.Extension.ToLower() != ".json" || File.Directory.FullName
                                .Replace(ModBase.pathHelpFolder.TrimEnd('\\'), "").Contains(@"\."))
                            continue;
                        // 检查忽略列表
                        var realPath = File.FullName.Replace(ModBase.pathHelpFolder.TrimEnd('\\'), "");
                        foreach (var Ignore in ignoreList)
                            if (realPath.RegexCheck(Ignore))
                            {
                                if (ModBase.modeDebug)
                                    ModBase.Log("[Help] 已忽略 " + realPath + "：" + Ignore);
                                goto NextFile;
                            }

                        fileList.Add(File.FullName);
                        NextFile: ;
                    }

                    ModBase.Log("[Help] 已扫描缓存文件夹下的帮助文件，目前总计 " + fileList.Count + " 条");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "检查帮助文件夹失败", ModBase.LogLevel.Msgbox);
                }

                if (Loader.IsAborted)
                    return;

                // 将文件实例化
                var dict = new List<HelpEntry>();
                foreach (var FilePath in fileList)
                    try
                    {
                        var entry = new HelpEntry(FilePath);
                        dict.Add(entry);
                        if (ModBase.modeDebug)
                            ModBase.Log("[Help] 已加载的帮助条目：" + entry.title + " ← " + FilePath);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "初始化帮助条目失败（" + FilePath + "）", ModBase.LogLevel.Msgbox);
                    }

                // 回设
                if (!dict.Any())
                    throw new Exception("未找到可用的帮助；若不需要帮助页面，可以在 设置 → 个性化 → 功能隐藏 中将其隐藏");
                if (Loader.IsAborted)
                    return;
                Loader.output = dict;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "帮助列表初始化失败");
                throw;
            }
        }
    }

    /// <summary>
    ///     解压内置帮助文件。
    /// </summary>
    public static void HelpExtract()
    {
        ModBase.DeleteDirectory(ModBase.pathTemp + @"CE\Help");
        Directory.CreateDirectory(ModBase.pathTemp + @"CE\Help");
        ModBase.WriteFile(ModBase.pathTemp + @"CE\Cache\Help.zip", ModBase.GetResourceStream("Resources/Help.zip"));
        ModBase.ExtractFile(ModBase.pathTemp + @"CE\Cache\Help.zip", ModBase.pathTemp + @"CE\Help", Encoding.UTF8);
        ModBase.Log("[Help] 已解压内置帮助文件，目前状态：" + File.Exists(ModBase.pathTemp + @"CE\Help\启动器\备份设置.xaml"),
            ModBase.LogLevel.Debug);
    }

    /// <summary>
    ///     对帮助文件约定的替换标记进行处理，如果遇到需要转义的字符会进行转义。
    /// </summary>
    public static string HelpArgumentReplace(string Xaml)
    {
        var result = Xaml.Replace("{path}", ModBase.EscapeXML(ModBase.exePath));
        result = result.RegexReplaceEach(@"\{hint\}", _ => ModBase.EscapeXML(PageToolsTest.GetRandomHint()));
        result = result.RegexReplaceEach(@"\{cave\}", _ => ModBase.EscapeXML(PageToolsTest.GetRandomCave()));
        return result;
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
    public static void ShowWindowToTop(nint Handle)
    {
        try
        {
            PostMessage(Handle, 400 * 16 + 2, 0L, 0L);
            SetForegroundWindow(Handle); // 不在这里放不行，神秘 WinAPI，建议别动
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置窗口置顶失败", ModBase.LogLevel.Hint);
        }
    }

    [DllImport("user32", EntryPoint = "FindWindowA")]
    public static extern nint FindWindow(string ClassName, string WindowName);

    [DllImport("user32")]
    public static extern int SetForegroundWindow(nint hWnd);

    [DllImport("user32", EntryPoint = "PostMessageA")]
    private static extern bool PostMessage(nint hWnd, uint msg, long wParam, long lParam);

    /// <summary>
    ///     将特定程序设置为使用高性能显卡启动。
    ///     如果失败，则抛出异常。
    /// </summary>
    public static void SetGPUPreference(string Executeable, bool WantHighPerformance = true)
    {
        const string gPU_PERFERENCE_REG_KEY = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string gPU_PERFERENCE_REG_VALUE_HIGH = "GpuPreference=2;";
        const string gPU_PERFERENCE_REG_VALUE_DEFAULT = "GpuPreference=0;";
        // Const GPU_PERFERENCE_REG_VALUE_POWER_SAVING As String = "GpuPreference=1;"

        var isCurrentHighPerformance = false;
        // 查看现有设置
        // 就知道 My.Computer，改个注册表 Microsoft.Win32.Registry 几年前的 API 了不用，还在这 My.Computer 都 5202 年了 My 你大爷
        using (var readOnlyKey = Registry.CurrentUser.OpenSubKey(gPU_PERFERENCE_REG_KEY, false))
        {
            if (readOnlyKey is not null)
            {
                var currentValue = readOnlyKey.GetValue(Executeable);
                if (gPU_PERFERENCE_REG_VALUE_HIGH == (currentValue?.ToString() ?? "")) isCurrentHighPerformance = true;
            }
            else
            {
                // 创建父级键
                ModBase.Log("[System] 需要创建显卡设置的父级键");
                Registry.CurrentUser.CreateSubKey(gPU_PERFERENCE_REG_KEY);
            }
        }

        ModBase.Log($"[System] 当前程序 ({Executeable}) 的显卡设置为高性能: {isCurrentHighPerformance}");
        if (isCurrentHighPerformance ^ WantHighPerformance)
            // 写入新设置
            using (var writeKey = Registry.CurrentUser.OpenSubKey(gPU_PERFERENCE_REG_KEY, true))
            {
                writeKey.SetValue(Executeable,
                    WantHighPerformance ? gPU_PERFERENCE_REG_VALUE_HIGH : gPU_PERFERENCE_REG_VALUE_DEFAULT);
                ModBase.Log($"[System] 已调整程序 ({Executeable}) 显卡设置: {WantHighPerformance}");
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
    text = text.Replace("{minecraft}", replacer(ModMinecraft.mcFolderSelected));
    
    if (ModMinecraft.McInstanceSelected is not null)
    {
        text = text.Replace("{version_path}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
        text = text.Replace("{verpath}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
        text = text.Replace("{version_indie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
        text = text.Replace("{verindie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
        text = text.Replace("{name}", replacer(ModMinecraft.McInstanceSelected.Name));
        
        if (new[] { "unknown", "old", "pending" }.Contains(ModMinecraft.McInstanceSelected.Info.vanillaName))
        {
            text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Name));
        }
        else
        {
            text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Info.vanillaName));
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
        text = text.Replace("{user}", replacer(ModLaunch.mcLoginLoader.output.name));
        text = text.Replace("{uuid}", replacer(ModLaunch.mcLoginLoader.output.uuid.ToLower()));
        
        switch (ModLaunch.mcLoginLoader.input.type)
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
            return replacer(item.GetValueNoType(ModMinecraft.McInstanceSelected?.PathInstance)?.ToString() ?? "");
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
    /// <param name="RequireNonSpace">是否要求路径不包含空格。</param>
    public static string RequestTaskTempFolder(bool RequireNonSpace = false)
    {
        TryClearTaskTemp();
        string resultFolder;
        do
        {
            try
            {
                resultFolder = $@"{ModBase.pathTemp}TaskTemp\{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";
                if (RequireNonSpace && resultFolder.Contains(" "))
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