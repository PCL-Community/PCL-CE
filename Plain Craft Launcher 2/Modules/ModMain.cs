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
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;

namespace PCL;

public static class ModMain
{
    public static FormMain? FrmMain;
    public static SplashScreen? FrmStart;
    public static PageLaunchLeft? FrmLaunchLeft;
    public static PageLaunchRight? FrmLaunchRight;
    public static PageLogLeft? FrmLogLeft;
    public static PageLogRight? FrmLogRight;
    public static PageSelectLeft? FrmSelectLeft;
    public static PageSelectRight? FrmSelectRight;
    public static PageSpeedLeft? FrmSpeedLeft;
    public static PageSpeedRight? FrmSpeedRight;
    public static PageToolsLeft? FrmToolsLeft;
    public static PageToolsGameLink? FrmToolsGameLink;
    public static PageToolsHelp? FrmToolsHelp;
    public static PageToolsTest? FrmToolsTest;
    public static PageDownloadLeft? FrmDownloadLeft;
    public static PageDownloadInstall? FrmDownloadInstall;
    public static PageDownloadClient? FrmDownloadClient;
    public static PageDownloadOptiFine? FrmDownloadOptiFine;
    public static PageDownloadLiteLoader? FrmDownloadLiteLoader;
    public static PageDownloadForge? FrmDownloadForge;
    public static PageDownloadNeoForge? FrmDownloadNeoForge;
    public static PageDownloadCleanroom? FrmDownloadCleanroom;
    public static PageDownloadFabric? FrmDownloadFabric;
    public static PageDownloadQuilt? FrmDownloadQuilt;
    public static PageDownloadLabyMod? FrmDownloadLabyMod;
    public static PageDownloadLegacyFabric? FrmDownloadLegacyFabric;
    public static PageDownloadMod? FrmDownloadMod;
    public static PageDownloadPack? FrmDownloadPack;
    public static PageDownloadDataPack? FrmDownloadDataPack;
    public static PageDownloadShader? FrmDownloadShader;
    public static PageDownloadResourcePack? FrmDownloadResourcePack;
    public static PageDownloadWorld? FrmDownloadWorld;
    public static PageDownloadCompFavorites? FrmDownloadCompFavorites;
    public static PageSetupLeft? FrmSetupLeft;
    public static PageSetupLaunch? FrmSetupLaunch;
    public static PageSetupUI? FrmSetupUI;
    public static PageSetupGameManage? FrmSetupGameManage;
    public static PageSetupUpdate? FrmSetupUpdate;
    public static PageSetupJava? FrmSetupJava;
    public static PageHomePageMarket? FrmHomePageMarket;
    public static PageSetupAbout? FrmSetupAbout;
    public static PageSetupLog? FrmSetupLog;
    public static PageSetupFeedback? FrmSetupFeedback;
    public static PageSetupGameLink? FrmSetupGameLink;
    public static PageSetupLauncherMisc? FrmSetupLauncherMisc;
    public static PageLoginAuth? FrmLoginAuth;
    public static PageLoginMs? FrmLoginMs;
    public static PageLoginProfile? FrmLoginProfile;
    public static PageLoginProfileSkin? FrmLoginProfileSkin;
    public static PageLoginOffline? FrmLoginOffline;
    public static PageInstanceLeft? FrmInstanceLeft;
    public static PageInstanceOverall? FrmInstanceOverall;
    public static PageInstanceCompResource? FrmInstanceMod;
    public static PageInstanceModDisabled? FrmInstanceModDisabled;
    public static PageInstanceScreenshot? FrmInstanceScreenshot;
    public static PageInstanceSaves? FrmInstanceSaves;
    public static PageInstanceCompResource? FrmInstanceShader;
    public static PageInstanceCompResource? FrmInstanceSchematic;
    public static PageInstanceCompResource? FrmInstanceResourcePack;
    public static PageInstanceSetup? FrmInstanceSetup;
    public static PageInstanceInstall? FrmInstanceInstall;
    public static PageInstanceExport? FrmInstanceExport;
    public static PageInstanceServer? FrmInstanceServer;
    public static PageInstanceSavesLeft? FrmInstanceSavesLeft;
    public static PageInstanceSavesInfo? FrmInstanceSavesInfo;
    public static PageInstanceSavesBackup? FrmInstanceSavesBackup;
    public static PageInstanceSavesDatapack? FrmInstanceSavesDatapack;
    public static PageDownloadCompDetail? FrmDownloadCompDetail;
    public static PageHomepageNewsView? FrmHomepageNews;

    public static ModLoader.LoaderTask<int, List<HelpEntry>> HelpLoader = new("Help Page", HelpLoad, null,
        ThreadPriority.BelowNormal);

    public static MySlider? DragControl = null;
    private static int Timer4Count;
    private static int Timer150Count;

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
            FrmMain!.DragTick();
            ModLoader.LoaderTaskbarProgressRefresh();
        }

        #endregion

        catch (Exception ex)
        {
            ModBase.Log(ex, "短程主时钟执行异常", ModBase.LogLevel.Critical);
        }

        Timer4Count += 1;
        if (Timer4Count == 4)
        {
            Timer4Count = 0;
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

        Timer150Count += 1;
        if (Timer150Count == 150)
        {
            Timer150Count = 0;
            try
            {
                #region 每 7.5s 执行一次的代码

                if (FrmMain!.BtnExtraApril_ShowCheck() && AprilDistance != 0)
                    FrmMain.BtnExtraApril.Ribble();
                // 以未知原因窗口被丢到一边去的修复（Top、Left = -25600），还有 #745
                ModBase.RunInUi(() =>
                {
                    if (!FrmMain.Hidden)
                    {
                        if (FrmMain.Top < -9000) FrmMain.Top = 100d;
                        if (FrmMain.Left < -9000) FrmMain.Left = 100d;
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
        if (!IsAprilEnabled)
            return;
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var LastTime = Environment.TickCount;
                while (true)
                {
                    if (LastTime != Environment.TickCount)
                    {
                        LastTime = Environment.TickCount;
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
    ///     在窗口左下角弹出提示文本。
    /// </summary>
    public static void Hint(string? Text, HintType Type = HintType.Info, bool Log = true)
    {
        HintWaiting.Add(new HintMessage { Text = Text ?? "", Type = Type, Log = Log });
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
                var CurrentHint = HintWaiting[0];
                // 去回车
                CurrentHint.Text = CurrentHint.Text.Replace("\r\n", " ").Replace("\r", " ")
                    .Replace("\n", " ");
                // 超量提示直接忽略
                if (FrmMain!.PanHint.Children.Count >= 20)
                    goto EndHint;
                // 检查是否有重复提示
                Border? DoubleStack = null;
                foreach (Border stack in FrmMain.PanHint.Children)
                    if (stack.Tag is object[] tagArray && (bool)tagArray[0] &&
                                              (((TextBlock)stack.Child).Text ?? "") == (CurrentHint.Text ?? ""))
                        DoubleStack = stack;
                // 获取渐变颜色
                ModBase.MyColor TargetColor0, TargetColor1;
                var Percent = 0.3d;
                switch (CurrentHint.Type)
                {
                    case HintType.Info:
                    {
                        TargetColor0 = new ModBase.MyColor(215d, 37d, 155d, 252d);
                        TargetColor1 = new ModBase.MyColor(215d, 10d, 142d, 252d);
                        break;
                    }
                    case HintType.Finish:
                    {
                        TargetColor0 = new ModBase.MyColor(215d, 33d, 177d, 33d);
                        TargetColor1 = new ModBase.MyColor(215d, 29d, 160d, 29d); // HintType.Critical
                        break;
                    }

                    default:
                    {
                        TargetColor0 = new ModBase.MyColor(215d, 255d, 53d, 11d);
                        TargetColor1 = new ModBase.MyColor(215d, 255d, 43d, 0d);
                        break;
                    }
                }

                if (DoubleStack != null)
                {
                    var doubleStackTag = (object[])DoubleStack.Tag;
                    // 有重复提示，且该提示的进入动画已播放
                    if (!ModAnimation.AniIsRun($"Hint Show {doubleStackTag[1]}"))
                    {
                        ModAnimation.AniStop($"Hint Hide {doubleStackTag[1]}");
                        var Delay = (800d + ModBase.MathClamp(CurrentHint.Text!.Length, 5d, 23d) * 180d) *
                                    ModAnimation.AniSpeed;
                        ModAnimation.AniStart(new[]
                            {
                                ModAnimation.AaX(DoubleStack, -12 - DoubleStack.Margin.Left, 50,
                                    Ease: new ModAnimation.AniEaseOutFluent()),
                                ModAnimation.AaX(DoubleStack, -8, 50, 50, new ModAnimation.AniEaseInFluent()),
                                ModAnimation.AaX(DoubleStack, 8d, 50, 100, new ModAnimation.AniEaseOutFluent()),
                                ModAnimation.AaX(DoubleStack, -8, 50, 150, new ModAnimation.AniEaseInFluent()),
                                ModAnimation.AaDouble(i =>
                                {
                                    Percent += (double)i;
                                    var Gradient = (LinearGradientBrush)DoubleStack.Background;
                                    Gradient.GradientStops[0].Color = TargetColor0 * Percent +
                                                                      new ModBase.MyColor(255d, 255d, 255d) *
                                                                      (1d - Percent);
                                    Gradient.GradientStops[1].Color = TargetColor1 * Percent +
                                                                      new ModBase.MyColor(255d, 255d, 255d) *
                                                                      (1d - Percent);
                                }, 0.7d, 250),
                                ModAnimation.AaX(DoubleStack, -50, 200, (int)Math.Round(Delay),
                                    new ModAnimation.AniEaseInFluent()),
                                ModAnimation.AaOpacity(DoubleStack, -1, 150, (int)Math.Round(Delay)),
                                ModAnimation.AaCode(() => doubleStackTag[0] = false,
                                    (int)Math.Round(Delay)),
                                ModAnimation.AaHeight(DoubleStack, -26, 100, Ease: new ModAnimation.AniEaseOutFluent(),
                                    After: true),
                                ModAnimation.AaCode(() => FrmMain.PanHint.Children.Remove(DoubleStack), After: true)
                            },
                            $"Hint Hide {doubleStackTag[1]}");
                    }
                }
                else
                {
                    // 准备控件
                    var newHintTag = new object[] { true, ModBase.GetUuid() };
                    var NewHintControl = new Border
                    {
                        Tag = newHintTag, Margin = new Thickness(-70, 0d, 20d, 0d),
                        Opacity = 0d,
                        Height = 0d, HorizontalAlignment = HorizontalAlignment.Left,
                        CornerRadius = new CornerRadius(0d, 6d, 6d, 0d),
                        Background = new LinearGradientBrush(
                            new GradientStopCollection(new List<GradientStop>
                            {
                                new(TargetColor0 * Percent + new ModBase.MyColor(255d, 255d, 255d) * (1d - Percent),
                                    0d),
                                new(TargetColor1 * Percent + new ModBase.MyColor(255d, 255d, 255d) * (1d - Percent), 1d)
                            }), 90d),
                        Child = new TextBlock
                        {
                            TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13d, Text = CurrentHint.Text,
                            Foreground = new ModBase.MyColor(255d, 255d, 255d), Margin = new Thickness(33d, 5d, 8d, 5d)
                        }
                    };
                    // AddHandler NewHintControl.MouseLeftButtonDown, AddressOf HideAllHint
                    FrmMain.PanHint.Children.Add(NewHintControl);
                    // 控件动画
                    var Animations = new List<ModAnimation.AniData>();
                    if (FrmMain.PanHint.Children.Count > 1)
                        // 已有提示
                        Animations.Add(ModAnimation.AaHeight(NewHintControl, 26d, 150,
                            Ease: new ModAnimation.AniEaseOutFluent()));
                    else
                        // 是唯一提示
                        NewHintControl.Height = 26d;
                    // 开始动画
                    Animations.AddRange([
                        ModAnimation.AaX(NewHintControl, 30d,
                            Ease: new ModAnimation.AniEaseOutElastic(ModAnimation.AniEasePower.Weak)),
                        ModAnimation.AaX(NewHintControl, 20d, 200, Ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaOpacity(NewHintControl, 1d, 100),
                        ModAnimation.AaDouble(i =>
                        {
                            Percent += (double)i;
                            var Gradient = (LinearGradientBrush)NewHintControl.Background;
                            Gradient.GradientStops[0].Color = TargetColor0 * Percent +
                                                              new ModBase.MyColor(255d, 255d, 255d) * (1d - Percent);
                            Gradient.GradientStops[1].Color = TargetColor1 * Percent +
                                                              new ModBase.MyColor(255d, 255d, 255d) * (1d - Percent);
                        }, 0.7d, 250, 100)
                    ]);
                    ModAnimation.AniStart(Animations, $"Hint Show {newHintTag[1]}");
                    // 结束动画
                    var Delay = (800d + ModBase.MathClamp(CurrentHint.Text!.Length, 5d, 23d) * 180d) *
                                ModAnimation.AniSpeed;
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaX(NewHintControl, -50, 200, (int)Math.Round(Delay),
                                new ModAnimation.AniEaseInFluent()),
                            ModAnimation.AaOpacity(NewHintControl, -1, 150, (int)Math.Round(Delay)),
                            ModAnimation.AaCode(() => newHintTag[0] = false, (int)Math.Round(Delay)),
                            ModAnimation.AaHeight(NewHintControl, -26, 100, Ease: new ModAnimation.AniEaseOutFluent(),
                                After: true),
                            ModAnimation.AaCode(() => FrmMain.PanHint.Children.Remove(NewHintControl), After: true)
                        }, $"Hint Hide {newHintTag[1]}");
                }

                // 结束处理
                EndHint: ;

                if (CurrentHint.Log)
                    ModBase.Log("[UI] 弹出提示：" + CurrentHint.Text);
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
        foreach (Border Control in FrmMain!.PanHint.Children)
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
                    ModAnimation.AaCode(() => FrmMain.PanHint.Children.Remove(Control), After: true)
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
        public object AuthUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        public string Button1 = "确定";

        /// <summary>
        ///     点击第一个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action Button1Action;

        public string Button2 = "";

        /// <summary>
        ///     点击第二个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action Button2Action;

        public string Button3 = "";

        /// <summary>
        ///     点击第三个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action Button3Action;

        /// <summary>
        ///     输入模式：文本框的文本。
        ///     选择模式：需要放进去的 List(Of MyListItem)。
        ///     登录模式：登录步骤 1 中返回的 JSON。
        /// </summary>
        public object Content;

        public bool ForceWait;

        /// <summary>
        ///     有多个按钮时，是否给第一个按钮加高亮。
        /// </summary>
        public bool HighLight;

        /// <summary>
        ///     输入模式：提示文本。
        /// </summary>
        public string HintText = "";

        /// <summary>
        ///     弹窗是否已经关闭。
        /// </summary>
        public bool IsExited = false;

        public bool IsWarn;

        /// <summary>
        ///     输入模式：输入的文本。若点击了 非 第一个按钮，则为 Nothing。
        ///     选择模式：点击的按钮编号，从 1 开始。
        ///     登录模式：字符串数组 {AccessToken, RefreshToken} 或一个 Exception。
        /// </summary>
        public object Result;

        public string Text;
        public string Title;
        public MyMsgBoxType Type;

        /// <summary>
        ///     输入模式：输入验证规则。
        /// </summary>
        public Collection<IValidator<string>> ValidateRules;

        public DispatcherFrame WaitFrame = new(true);
    }

    public enum MyMsgBoxType
    {
        Text,
        Select,
        Input,
        Login,
        Markdown
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
    public static int MyMsgBox(string Caption, string Title = "提示", string Button1 = "确定", string Button2 = "",
        string Button3 = "", bool IsWarn = false, bool HighLight = true, bool ForceWait = false,
        Action Button1Action = null, Action Button2Action = null, Action Button3Action = null)
    {
        // 将弹窗列入队列
        var Converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Text, Button1 = Button1, Button2 = Button2, Button3 = Button3, Text = Caption,
            IsWarn = IsWarn, Title = Title, HighLight = HighLight, ForceWait = true, Button1Action = Button1Action,
            Button2Action = Button2Action, Button3Action = Button3Action
        };
        WaitingMyMsgBox.Add(Converter);
        if (ModBase.RunInUi())
            // 若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick();
        if (Button2.Length > 0 || ForceWait)
        {
            // 若有多个按钮则开始等待
            if (FrmMain is null || (FrmMain.PanMsg is null && ModBase.RunInUi()))
            {
                // 主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(Converter);
                if (Button2.Length > 0)
                {
                    var RawResult = Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)(Button3.Length > 0 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo) +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    switch (RawResult)
                    {
                        case MsgBoxResult.Yes:
                        {
                            Converter.Result = 1;
                            break;
                        }
                        case MsgBoxResult.No:
                        {
                            Converter.Result = 2;
                            break;
                        }
                        case MsgBoxResult.Cancel:
                        {
                            Converter.Result = 3;
                            break;
                        }
                    }
                }
                else
                {
                    Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)MsgBoxStyle.OkOnly +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    Converter.Result = 1;
                }

                ModBase.Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" + Button1 + "," + Button2 + "," + Button3,
                    ModBase.LogLevel.Debug);
            }
            else
            {
                try
                {
                    FrmMain.DragStop();
                    ComponentDispatcher.PushModal();
                    Dispatcher.PushFrame(Converter.WaitFrame);
                }
                finally
                {
                    ComponentDispatcher.PopModal();
                }
            }

            ModBase.Log($"[Control] 普通弹框返回：{Converter.Result ?? "null"}");
            return (int)Converter.Result;
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
    public static int MyMsgBoxMarkdown(string Caption, string Title = "提示", string Button1 = "确定", string Button2 = "",
        string Button3 = "", bool IsWarn = false, bool HighLight = true, bool ForceWait = false,
        Action Button1Action = null, Action Button2Action = null, Action Button3Action = null)
    {
        // 将弹窗列入队列
        var Converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Markdown, Button1 = Button1, Button2 = Button2, Button3 = Button3, Text = Caption,
            IsWarn = IsWarn, Title = Title, HighLight = HighLight, ForceWait = true, Button1Action = Button1Action,
            Button2Action = Button2Action, Button3Action = Button3Action
        };
        WaitingMyMsgBox.Add(Converter);
        if (ModBase.RunInUi())
            // 若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick();
        if (Button2.Length > 0 || ForceWait)
        {
            // 若有多个按钮则开始等待
            if (FrmMain is null || (FrmMain.PanMsg is null && ModBase.RunInUi()))
            {
                // 主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(Converter);
                if (Button2.Length > 0)
                {
                    var RawResult = Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)(Button3.Length > 0 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo) +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    switch (RawResult)
                    {
                        case MsgBoxResult.Yes:
                        {
                            Converter.Result = 1;
                            break;
                        }
                        case MsgBoxResult.No:
                        {
                            Converter.Result = 2;
                            break;
                        }
                        case MsgBoxResult.Cancel:
                        {
                            Converter.Result = 3;
                            break;
                        }
                    }
                }
                else
                {
                    Interaction.MsgBox(Caption,
                        (MsgBoxStyle)((int)MsgBoxStyle.OkOnly +
                                      (int)(IsWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), Title);
                    Converter.Result = 1;
                }

                ModBase.Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" + Button1 + "," + Button2 + "," + Button3,
                    ModBase.LogLevel.Debug);
            }
            else
            {
                try
                {
                    FrmMain.DragStop();
                    ComponentDispatcher.PushModal();
                    Dispatcher.PushFrame(Converter.WaitFrame);
                }
                finally
                {
                    ComponentDispatcher.PopModal();
                }
            }

            ModBase.Log($"[Control] 普通弹框返回：{Converter.Result ?? "null"}");
            return (int)Converter.Result;
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
        Collection<IValidator<string>>? ValidateRules = null, string HintText = "", string Button1 = "确定",
        string Button2 = "取消", bool IsWarn = false)
    {
        // 将弹窗列入队列
        var Converter = new MyMsgBoxConverter
        {
            Text = Text, HintText = HintText, Type = MyMsgBoxType.Input,
            ValidateRules = ValidateRules ?? [], Button1 = Button1, Button2 = Button2,
            Content = DefaultInput, IsWarn = IsWarn, Title = Title
        };
        WaitingMyMsgBox.Add(Converter);
        // 虽然我也不知道这是啥但是能用就成了 :)
        try
        {
            FrmMain?.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(Converter.WaitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        ModBase.Log($"[Control] 输入弹框返回：{Converter.Result}");
        return Converter.Result?.ToString();
    }

    /// <summary>
    ///     显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="Title">弹窗的标题。</param>
    /// <param name="Button1">显示的第一个按钮，默认为 “确定”。</param>
    /// <param name="Button2">显示的第二个按钮，默认为空。</param>
    /// <param name="IsWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int? MyMsgBoxSelect(List<IMyRadio> Selections, string Title = "提示", string Button1 = "确定",
        string Button2 = "", bool IsWarn = false)
    {
        // 将弹窗列入队列
        var Converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Select, Button1 = Button1, Button2 = Button2, Content = Selections, IsWarn = IsWarn,
            Title = Title
        };
        WaitingMyMsgBox.Add(Converter);
        // 虽然我也不知道这是啥但是能用就成了 :)
        try
        {
            if (FrmMain is not null)
                FrmMain.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(Converter.WaitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        ModBase.Log($"[Control] 选择弹框返回：{Converter.Result ?? "null"}");
        return (int?)Converter.Result;
    }


    public static void MyMsgBoxTick()
    {
        try
        {
            if (FrmMain is null || FrmMain.PanMsg is null || FrmMain.WindowState == WindowState.Minimized)
                return;
            if (FrmMain.PanMsg.Children.Count > 0)
            {
                // 弹窗中
                FrmMain.PanMsgBackground.Visibility = Visibility.Visible;
            }
            else if (WaitingMyMsgBox.Any())
            {
                // 没有弹窗，显示一个等待的弹窗
                FrmMain.PanMsgBackground.Visibility = Visibility.Visible;
                switch (WaitingMyMsgBox[0].Type)
                {
                    case MyMsgBoxType.Input:
                    {
                        FrmMain.PanMsg.Children.Add(new MyMsgInput(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Select:
                    {
                        FrmMain.PanMsg.Children.Add(new MyMsgSelect(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Text:
                    {
                        FrmMain.PanMsg.Children.Add(new MyMsgText(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Login:
                    {
                        FrmMain.PanMsg.Children.Add(new MyMsgLogin(WaitingMyMsgBox[0]));
                        break;
                    }
                    case MyMsgBoxType.Markdown:
                    {
                        FrmMain.PanMsg.Children.Add(new MyMsgMarkdown(WaitingMyMsgBox[0]));
                        break;
                    }
                }

                WaitingMyMsgBox.RemoveAt(0);
            }
            // 没有弹窗，没有等待的弹窗
            else if (!(FrmMain.PanMsgBackground.Visibility == Visibility.Collapsed))
            {
                FrmMain.PanMsgBackground.Visibility = Visibility.Collapsed;
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
        var btnText1 = buttons.Count < 1 ? "确定" : buttons.ElementAt(0).Context;
        var btnAct1 = (Action)(buttons.Count < 1 ? (object)null : buttons.ElementAt(0).OnClick);
        var btnText2 = buttons.Count < 2 ? "取消" : buttons.ElementAt(1).Context;
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
        public string Desc;

        public string EventData;
        public string EventType;

        // 动作

        /// <summary>
        ///     是否为 “执行事件”。
        /// </summary>
        public bool IsEvent;

        // 显示（可选）

        /// <summary>
        ///     帮助项的自定义图标。可能为 Nothing。
        /// </summary>
        public string Logo;

        /// <summary>
        ///     原始信息路径。用于刷新。
        /// </summary>
        public string RawPath;

        /// <summary>
        ///     检索关键字。
        /// </summary>
        public string Search;

        /// <summary>
        ///     是否在公开版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        /// </summary>
        public bool ShowInPublic = true;

        /// <summary>
        ///     是否显示在搜索结果。默认为 True。
        /// </summary>
        public bool ShowInSearch = true;

        /// <summary>
        ///     是否在快照版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        /// </summary>
        public bool ShowInSnapshot = true;

        // 基础

        /// <summary>
        ///     显示标题。
        /// </summary>
        public string Title;

        /// <summary>
        ///     用于分类的标签列表。
        /// </summary>
        public List<string> Types;

        /// <summary>
        ///     若非执行事件，其对应的 .xaml 本地文件内容。
        /// </summary>
        public string XamlContent;

        // 转换

        /// <summary>
        ///     从文件初始化 HelpEntry 对象，失败会抛出异常。
        /// </summary>
        public HelpEntry(string FilePath)
        {
            RawPath = FilePath;
            var JsonData = (JObject)ModBase.GetJson(ModMain.ArgumentReplace(ModBase.ReadFile(FilePath)));
            if (JsonData is null)
                throw new FileNotFoundException("未找到帮助文件：" + FilePath, FilePath);
            // 加载常规信息
            if (JsonData["Title"] is not null)
                Title = (string)JsonData["Title"];
            else
                throw new ArgumentException("未找到 Title 项");
            Desc = (string)(JsonData["Description"] ?? "");
            Search = (string)(JsonData["Keywords"] ?? "");
            Logo = (string)JsonData["Logo"]; // 为保持 Nothing，不要加 If
            ShowInSearch = (bool)(JsonData["ShowInSearch"] ?? ShowInSearch);
            ShowInPublic = (bool)(JsonData["ShowInPublic"] ?? ShowInPublic);
            ShowInSnapshot = (bool)(JsonData["ShowInSnapshot"] ?? ShowInSnapshot);
            Types = new List<string>();
            foreach (var NameOfType in (IEnumerable)(JsonData["Types"] ?? ModBase.GetJson("[]")))
                Types.Add(NameOfType.ToString());
            // 加载事件信息
            if ((bool)(JsonData["IsEvent"] ?? false))
            {
                EventType = Enum.Parse(typeof(CustomEvent.EventType), JsonData["EventType"].ToString()).ToString();
                EventData = (JsonData["EventData"] ?? "").ToString();
                IsEvent = true;
            }
            else
            {
                var XamlAddress = FilePath.ToLower().Replace(".json", ".xaml");
                if (File.Exists(XamlAddress))
                {
                    XamlContent = ModBase.ReadFile(XamlAddress);
                    IsEvent = false;
                }
                else
                {
                    throw new FileNotFoundException("未找到帮助条目 .json 对应的 .xaml 文件（" + XamlAddress + "）");
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
            string Logo;
            if (IsEvent)
            {
                if (EventType == "弹出窗口")
                    Logo = ModBase.PathImage + "Blocks/GrassPath.png";
                else
                    Logo = ModBase.PathImage + "Blocks/CommandBlock.png";
            }
            else
            {
                Logo = ModBase.PathImage + "Blocks/Grass.png";
            }

            // 设置属性
            Item.SnapsToDevicePixels = true;
            Item.Title = Title;
            Item.Info = Desc;
            Item.Logo = this.Logo ?? Logo;
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


    private static readonly object HelpLoadLock = new();

    /// <summary>
    ///     初始化帮助列表对象。
    /// </summary>
    private static void HelpLoad(ModLoader.LoaderTask<int, List<HelpEntry>> Loader)
    {
        lock (HelpLoadLock) // 避免重复解压文件导致出错
        {
            try
            {
                // 解压内置文件
                HelpExtract();

                // 遍历文件
                var FileList = new List<string>();
                try
                {
                    var IgnoreList = new List<string>();
                    // 读取自定义文件
                    if (Directory.Exists(ModBase.ExePath + @"PCL\Help\"))
                        foreach (var File in ModBase.EnumerateFiles(ModBase.ExePath + @"PCL\Help\"))
                            switch (File.Extension.ToLower() ?? "")
                            {
                                case ".helpignore":
                                {
                                    // 加载忽略列表
                                    ModBase.Log("[Help] 发现 .helpignore 文件：" + File.FullName);
                                    foreach (var Line in ModBase.ReadFile(File.FullName)
                                                 .Split("\r\n".ToCharArray()))
                                    {
                                        var RealString = Line.BeforeFirst("#").Trim();
                                        if (string.IsNullOrWhiteSpace(RealString))
                                            continue;
                                        IgnoreList.Add(RealString);
                                        if (ModBase.ModeDebug)
                                            ModBase.Log("[Help]  > " + RealString);
                                    }

                                    break;
                                }
                                case ".json":
                                {
                                    FileList.Add(File.FullName);
                                    break;
                                }
                            }

                    ModBase.Log("[Help] 已扫描 PCL 文件夹下的帮助文件，目前总计 " + FileList.Count + " 条");
                    // 读取自带文件
                    foreach (var File in ModBase.EnumerateFiles(ModBase.PathHelpFolder))
                    {
                        // 跳过非 Json 文件与以 . 开头的文件夹
                        if (File.Extension.ToLower() != ".json" || File.Directory.FullName
                                .Replace(ModBase.PathHelpFolder.TrimEnd('\\'), "").Contains(@"\."))
                            continue;
                        // 检查忽略列表
                        var RealPath = File.FullName.Replace(ModBase.PathHelpFolder.TrimEnd('\\'), "");
                        foreach (var Ignore in IgnoreList)
                            if (RealPath.RegexCheck(Ignore))
                            {
                                if (ModBase.ModeDebug)
                                    ModBase.Log("[Help] 已忽略 " + RealPath + "：" + Ignore);
                                goto NextFile;
                            }

                        FileList.Add(File.FullName);
                        NextFile: ;
                    }

                    ModBase.Log("[Help] 已扫描缓存文件夹下的帮助文件，目前总计 " + FileList.Count + " 条");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "检查帮助文件夹失败", ModBase.LogLevel.Msgbox);
                }

                if (Loader.IsAborted)
                    return;

                // 将文件实例化
                var Dict = new List<HelpEntry>();
                foreach (var FilePath in FileList)
                    try
                    {
                        var Entry = new HelpEntry(FilePath);
                        Dict.Add(Entry);
                        if (ModBase.ModeDebug)
                            ModBase.Log("[Help] 已加载的帮助条目：" + Entry.Title + " ← " + FilePath);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "初始化帮助条目失败（" + FilePath + "）", ModBase.LogLevel.Msgbox);
                    }

                // 回设
                if (!Dict.Any())
                    throw new Exception("未找到可用的帮助；若不需要帮助页面，可以在 设置 → 个性化 → 功能隐藏 中将其隐藏");
                if (Loader.IsAborted)
                    return;
                Loader.Output = Dict;
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
        ModBase.DeleteDirectory(ModBase.PathTemp + @"CE\Help");
        Directory.CreateDirectory(ModBase.PathTemp + @"CE\Help");
        ModBase.WriteFile(ModBase.PathTemp + @"CE\Cache\Help.zip", ModBase.GetResourceStream("Resources/Help.zip"));
        ModBase.ExtractFile(ModBase.PathTemp + @"CE\Cache\Help.zip", ModBase.PathTemp + @"CE\Help", Encoding.UTF8);
        ModBase.Log("[Help] 已解压内置帮助文件，目前状态：" + File.Exists(ModBase.PathTemp + @"CE\Help\启动器\备份设置.xaml"),
            ModBase.LogLevel.Debug);
    }

    /// <summary>
    ///     对帮助文件约定的替换标记进行处理，如果遇到需要转义的字符会进行转义。
    /// </summary>
    public static string HelpArgumentReplace(string Xaml)
    {
        var Result = Xaml.Replace("{path}", ModBase.EscapeXML(ModBase.ExePath));
        Result = Result.RegexReplaceEach(@"\{hint\}", _ => ModBase.EscapeXML(PageToolsTest.GetRandomHint()));
        Result = Result.RegexReplaceEach(@"\{cave\}", _ => ModBase.EscapeXML(PageToolsTest.GetRandomCave()));
        return Result;
    }

    #endregion

    #region 愚人节

    public static bool IsAprilEnabled = DateTime.Now.Month == 4 && DateTime.Now.Day == 1;
    public static bool IsAprilGiveup = false;
    private static Vector AprilSpeed = new(0d, 0d);
    private static int AprilIdieCount;
    private static Point AprilMousePosLast = new(0d, 0d);
    private static int AprilDistance;

    private static void TimerFool()
    {
        try
        {
            if (FrmLaunchLeft is null || FrmLaunchLeft.AprilPosTrans is null || FrmMain.lastMouseArg is null)
                return;
            if (IsAprilGiveup || FrmMain.PageCurrent != FormMain.PageType.Launch ||
                ModAnimation.AniControlEnabled != 0 || !FrmLaunchLeft.BtnLaunch.IsLoaded)
                return;

            // 计算是否空闲
            var MousePos = FrmMain.lastMouseArg.GetPosition(FrmMain);
            if (MousePos == AprilMousePosLast)
            {
                AprilIdieCount += 1;
            }
            else
            {
                AprilMousePosLast = MousePos;
                AprilIdieCount = 0;
            }

            // 计算躲避移动
            Vector Direction;
            double Distance;
            var ButtonWidth = FrmLaunchLeft.BtnLaunch.ActualWidth / 2d;
            var ButtonHeight = FrmLaunchLeft.BtnLaunch.ActualHeight / 2d;
            var Vec = (Vector)(FrmMain.lastMouseArg.GetPosition(FrmLaunchLeft.BtnLaunch) -
                               new Vector(ButtonWidth, ButtonHeight));
            var Dir = new Vector(Vec.X, Vec.Y);
            Dir.Normalize();
            Direction = -Dir;
            Distance = new Vector(Math.Max(0d, Math.Abs(Vec.X) - ButtonWidth),
                Math.Max(0d, Math.Abs(Vec.Y) - ButtonHeight)).Length;
            var BreathScale = Math.Sin(Timer150Count / 37.5d * Math.PI);
            var Acc = Math.Max(0d, BreathScale * 0.25d - 0.65d - Math.Log((Distance + 0.4d) / 200d)) * Direction; // 加速度
            // 计算回归移动
            if (AprilIdieCount >= 64 * 5)
            {
                var SafeDist = (Vector)(FrmMain.lastMouseArg.GetPosition(FrmMain.PanMain) -
                                        new Vector(ButtonWidth, FrmMain.PanMain.ActualHeight - ButtonHeight * 3d));
                var Back = new Vector(FrmLaunchLeft.AprilPosTrans.X, FrmLaunchLeft.AprilPosTrans.Y);
                if (SafeDist.Length > 250d && Back.Length > 0.4d)
                {
                    Acc -= Back * 0.0005d;
                    Back.Normalize();
                    Acc -= Back * 0.15d;
                }
            }

            // 回到边界
            var Relative = FrmLaunchLeft.BtnLaunch.TranslatePoint(new Point(0d, 0d), FrmMain.PanForm);
            if (Relative.X < -ButtonWidth * 2d)
            {
                FrmLaunchLeft.AprilPosTrans.X += FrmMain.PanForm.ActualWidth + ButtonWidth * 2d; // 离开左边界
                AprilSpeed.X -= 80d;
                if (Relative.Y < 0d)
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5d;
                else if (Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2d)
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5d;
            }
            else if (Relative.X > FrmMain.PanForm.ActualWidth)
            {
                FrmLaunchLeft.AprilPosTrans.X -= FrmMain.PanForm.ActualWidth + ButtonWidth * 2d; // 离开右边界
                AprilSpeed.X += 80d;
                if (Relative.Y < 0d)
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5d;
                else if (Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2d)
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5d;
            }
            else if (Relative.Y < -ButtonHeight * 2d)
            {
                FrmLaunchLeft.AprilPosTrans.Y += FrmMain.PanForm.ActualHeight + ButtonHeight * 2d; // 离开上边界
                AprilSpeed.Y -= 25d;
                if (Relative.X < 0d)
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2d;
                else if (Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2d)
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2d;
            }
            else if (Relative.Y > FrmMain.PanForm.ActualHeight)
            {
                FrmLaunchLeft.AprilPosTrans.Y -= FrmMain.PanForm.ActualHeight + ButtonHeight * 2d; // 离开下边界
                AprilSpeed.Y += 25d;
                if (Relative.X < 0d)
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2d;
                else if (Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2d)
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2d;
            }

            // 移动
            AprilSpeed = AprilSpeed * 0.8d + Acc;
            var SpeedValue = Math.Min(60d, AprilSpeed.Length);
            if (SpeedValue < 0.01d)
                return;
            AprilSpeed.Normalize();
            AprilSpeed *= SpeedValue;
            AprilDistance = (int)Math.Round(AprilDistance + SpeedValue);
            FrmLaunchLeft.AprilPosTrans.X += AprilSpeed.X;
            FrmLaunchLeft.AprilPosTrans.Y += AprilSpeed.Y;
            // 大小改变
            FrmLaunchLeft.AprilScaleTrans.ScaleX =
                ModBase.MathClamp(1d - (Math.Abs(Direction.X) - Math.Abs(Direction.Y)) * (SpeedValue / 160d), 0.2d,
                    1.8d);
            FrmLaunchLeft.AprilScaleTrans.ScaleY =
                ModBase.MathClamp(1d - (Math.Abs(Direction.Y) - Math.Abs(Direction.X)) * (SpeedValue / 100d), 0.2d,
                    1.8d);
            // 放弃提示
            if (AprilDistance > 4000)
            {
                AprilDistance = -4000;
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
        const string GPU_PERFERENCE_REG_KEY = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string GPU_PERFERENCE_REG_VALUE_HIGH = "GpuPreference=2;";
        const string GPU_PERFERENCE_REG_VALUE_DEFAULT = "GpuPreference=0;";
        // Const GPU_PERFERENCE_REG_VALUE_POWER_SAVING As String = "GpuPreference=1;"

        var IsCurrentHighPerformance = false;
        // 查看现有设置
        // 就知道 My.Computer，改个注册表 Microsoft.Win32.Registry 几年前的 API 了不用，还在这 My.Computer 都 5202 年了 My 你大爷
        using (var ReadOnlyKey = Registry.CurrentUser.OpenSubKey(GPU_PERFERENCE_REG_KEY, false))
        {
            if (ReadOnlyKey is not null)
            {
                var CurrentValue = ReadOnlyKey.GetValue(Executeable);
                if (GPU_PERFERENCE_REG_VALUE_HIGH == (CurrentValue?.ToString() ?? "")) IsCurrentHighPerformance = true;
            }
            else
            {
                // 创建父级键
                ModBase.Log("[System] 需要创建显卡设置的父级键");
                Registry.CurrentUser.CreateSubKey(GPU_PERFERENCE_REG_KEY);
            }
        }

        ModBase.Log($"[System] 当前程序 ({Executeable}) 的显卡设置为高性能: {IsCurrentHighPerformance}");
        if (IsCurrentHighPerformance ^ WantHighPerformance)
            // 写入新设置
            using (var WriteKey = Registry.CurrentUser.OpenSubKey(GPU_PERFERENCE_REG_KEY, true))
            {
                WriteKey.SetValue(Executeable,
                    WantHighPerformance ? GPU_PERFERENCE_REG_VALUE_HIGH : GPU_PERFERENCE_REG_VALUE_DEFAULT);
                ModBase.Log($"[System] 已调整程序 ({Executeable}) 显卡设置: {WantHighPerformance}");
            }
    }

    /// <summary>
    /// 对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    /// /// </summary>
    public static string ArgumentReplace(string text, Func<string, string> escapeHandler = null, bool replaceTime = true) 
    {
    // 预处理
    if (text == null) return null;
    
    Func<string, string> replacer = (s) =>
    {
        if (s == null) return "";
        if (escapeHandler == null) return s;
        if (s.Contains(":\\")) s = ModBase.ShortenPath(s);
        return escapeHandler(s);
    };
    
    // 基础
    text = text.Replace("{pcl_version}", replacer(ModBase.VersionBaseName));
    text = text.Replace("{pcl_version_code}", replacer(ModBase.VersionCode.ToString()));
    text = text.Replace("{pcl_version_branch}", replacer(ModBase.VersionBranchName));
    text = text.Replace("{pcl_branch}", replacer(ModBase.VersionBranchName));
    text = text.Replace("{identify}", replacer(ModBase.UniqueAddress));
    text = text.Replace("{path}", replacer(Basics.ExecutableDirectory));
    text = text.Replace("{path_with_name}", replacer(Basics.ExecutableName));
    text = text.Replace("{path_temp}", replacer(ModBase.PathTemp));
    
    // 时间
    if (replaceTime) // 在窗口标题中，时间会被后续动态替换，所以此时不应该替换
    {
        text = text.Replace("{date}", replacer(DateTime.Now.ToString("yyyy/M/d")));
        text = text.Replace("{time}", replacer(DateTime.Now.ToString("HH:mm:ss")));
    }
    
    // Minecraft
    text = text.Replace("{java}", replacer(ModLaunch.McLaunchJavaSelected?.Installation.JavaFolder));
    text = text.Replace("{minecraft}", replacer(ModMinecraft.McFolderSelected));
    
    if (ModMinecraft.McInstanceSelected != null)
    {
        text = text.Replace("{version_path}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
        text = text.Replace("{verpath}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
        text = text.Replace("{version_indie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
        text = text.Replace("{verindie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
        text = text.Replace("{name}", replacer(ModMinecraft.McInstanceSelected.Name));
        
        if (new[] { "unknown", "old", "pending" }.Contains(ModMinecraft.McInstanceSelected.Info.VanillaName))
        {
            text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Name));
        }
        else
        {
            text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Info.VanillaName));
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
    if (ModLaunch.McLoginLoader.State == ModBase.LoadState.Finished)
    {
        text = text.Replace("{user}", replacer(ModLaunch.McLoginLoader.Output.Name));
        text = text.Replace("{uuid}", replacer(ModLaunch.McLoginLoader.Output.Uuid.ToLower()));
        
        switch (ModLaunch.McLoginLoader.Input.Type)
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
    text = ModBase.RegexReplaceEach(text, @"\{setup:([a-zA-Z0-9]+)\}", m => replacer(ModBase.Setup.GetSafe(m.Groups[1].Value, ModMinecraft.McInstanceSelected)?.ToString() ?? ""));
    text = ModBase.RegexReplaceEach(text, @"\{varible:([^\}]+)\}", m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value)));
    text = ModBase.RegexReplaceEach(text, @"\{variable:([^\}]+)\}", m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value)));
    
    return text;
}
    #endregion

    #region 任务缓存

    private static bool IsTaskTempCleared;
    private static bool IsTaskTempClearing;

    /// <summary>
    ///     尝试清理任务缓存文件夹。
    ///     在整次运行中只会实际清理一次。
    /// </summary>
    public static void TryClearTaskTemp()
    {
        if (!IsTaskTempCleared)
        {
            IsTaskTempCleared = true;
            IsTaskTempClearing = true;
            try
            {
                ModBase.Log("[System] 开始清理任务缓存文件夹");
                ModBase.DeleteDirectory($@"{ModBase.OsDrive}ProgramData\PCL\TaskTemp\");
                ModBase.DeleteDirectory($@"{ModBase.PathTemp}TaskTemp\");
                ModBase.Log("[System] 已清理任务缓存文件夹");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清理任务缓存文件夹失败");
            }
            finally
            {
                IsTaskTempClearing = false;
            }
        }
        else if (IsTaskTempClearing)
        {
            // 等待另一个清理步骤完成
            while (IsTaskTempClearing)
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
        string ResultFolder;
        do
        {
            try
            {
                ResultFolder = $@"{ModBase.PathTemp}TaskTemp\{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";
                if (RequireNonSpace && ResultFolder.Contains(" "))
                    break; // 带空格
                Directory.CreateDirectory(ResultFolder);
                ModBase.CheckPermissionWithException(ResultFolder);
                return ResultFolder;
            }
            catch
            {
            }
        } while (false);

        // 使用备用路径
        ResultFolder =
            $@"{ModBase.OsDrive}ProgramData\PCL\TaskTemp\{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";
        Directory.CreateDirectory(ResultFolder);
        ModBase.CheckPermission(ResultFolder);
        return ResultFolder;
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