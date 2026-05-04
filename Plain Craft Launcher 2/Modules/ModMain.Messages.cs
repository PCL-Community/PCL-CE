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

public static partial class ModMain
{
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

    static ModMain()
    {
        RegisterFeedbackSink();
    }

    public static void RegisterFeedbackSink()
    {
        LauncherFeedback.Sink ??= new ModMainFeedbackSink();
    }

    private sealed class ModMainFeedbackSink : ILauncherFeedbackSink
    {
        public void ShowHint(string text, HintKind kind)
        {
            Hint(text, kind switch
            {
                HintKind.Finish => HintType.Finish,
                HintKind.Critical => HintType.Critical,
                _ => HintType.Info
            }, false);
        }

        public int ShowMessage(string text, string title, string button1, string button2, bool isWarning)
        {
            return MyMsgBox(text, title, button1, button2, IsWarn: isWarning);
        }

        public bool CanFeedback(bool showHint)
        {
            var stat = UpdateManager.GetVersionStatus();
            if (stat == UpdateEnums.VersionStatus.Latest)
                return true;
            if (!showHint)
                return false;

            if (MyMsgBox(
                    stat == UpdateEnums.VersionStatus.NotLatest
                        ? $"你的 PCL 不是最新版，因此无法提交反馈。{"\r\n"}请在更新后，确认该问题在最新版中依然存在，然后再提交反馈。"
                        : $"你的 PCL 检查更新失败，因此无法提交反馈。{"\r\n"}请连接到互联网，在检查更新后，确认该问题在最新版中依然存在，然后再提交反馈。",
                    "无法提交反馈", stat == UpdateEnums.VersionStatus.NotLatest ? "更新" : "重新检查更新", "取消") == 1)
                NavigateToUpdatePage();

            return false;
        }

        public void NavigateToUpdatePage()
        {
            FrmMain?.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);
        }
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
}
