using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static partial class ModMain
{
    /// <summary>
    ///     等待显示的弹窗。
    /// </summary>
    public static List<MyMsgBoxConverter> WaitingMyMsgBox { get; } = [];

    #region 弹窗

    /// <summary>
    ///     存储弹窗信息的转换器。
    /// </summary>
    public class MyMsgBoxConverter
    {
        // 设置轮询 Url
        public object AuthUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        public string Button1 = "";

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

        public int[] ButtonIds = [1, 2, 3];

        public Action<int>? OnCloseCallback;
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
    /// <param name="title">弹窗的标题。</param>
    /// <param name="caption">弹窗的内容。</param>
    /// <param name="button1">显示的第一个按钮，默认为"确定"。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="button3">显示的第三个按钮，默认为空。</param>
    /// <param name="button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int MyMsgBox(string caption, string? title = null, string? button1 = null, string? button2 = "",
        string? button3 = "", bool isWarn = false, bool highLight = true, bool forceWait = false,
        Action button1Action = null, Action button2Action = null, Action button3Action = null)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        button3 ??= "";
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Text, Button1 = button1, Button2 = button2, Button3 = button3, Text = caption,
            IsWarn = isWarn, Title = title, HighLight = highLight, ForceWait = true, Button1Action = button1Action,
            Button2Action = button2Action, Button3Action = button3Action
        };
        WaitingMyMsgBox.Add(converter);
        if (ModBase.RunInUi())
            // 若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick();
        if (button2.Length > 0 || forceWait)
        {
            // 若有多个按钮则开始等待
            if (frmMain is null || (frmMain.PanMsg is null && ModBase.RunInUi()))
            {
                // 主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(converter);
                if (button2.Length > 0)
                {
                    var rawResult = Interaction.MsgBox(caption,
                        (MsgBoxStyle)((int)(button3.Length > 0 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo) +
                                      (int)(isWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), title);
                    switch (rawResult)
                    {
                        case MsgBoxResult.Yes:
                        {
                            converter.Result = 1;
                            break;
                        }
                        case MsgBoxResult.No:
                        {
                            converter.Result = 2;
                            break;
                        }
                        case MsgBoxResult.Cancel:
                        {
                            converter.Result = 3;
                            break;
                        }
                    }
                }
                else
                {
                    Interaction.MsgBox(caption,
                        (MsgBoxStyle)((int)MsgBoxStyle.OkOnly +
                                      (int)(isWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), title);
                    converter.Result = 1;
                }

                ModBase.Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" + button1 + "," + button2 + "," + button3,
                    ModBase.LogLevel.Debug);
            }
            else
            {
                try
                {
                    frmMain.DragStop();
                    ComponentDispatcher.PushModal();
                    Dispatcher.PushFrame(converter.WaitFrame);
                }
                finally
                {
                    ComponentDispatcher.PopModal();
                }
            }

            ModBase.Log($"[Control] 普通弹框返回：{converter.Result ?? "null"}");
            return (int)converter.Result;
        }

        // 不进行等待，直接返回
        return 1;
    }

    /// <summary>
    ///     显示弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="caption">弹窗的内容。</param>
    /// <param name="button1">显示的第一个按钮，默认为"确定"。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="button3">显示的第三个按钮，默认为空。</param>
    /// <param name="button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int MyMsgBoxMarkdown(string caption, string? title = null, string? button1 = null, string? button2 = "",
        string? button3 = "", bool isWarn = false, bool highLight = true, bool forceWait = false,
        Action button1Action = null, Action button2Action = null, Action button3Action = null)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        button3 ??= "";
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Markdown, Button1 = button1, Button2 = button2, Button3 = button3, Text = caption,
            IsWarn = isWarn, Title = title, HighLight = highLight, ForceWait = true, Button1Action = button1Action,
            Button2Action = button2Action, Button3Action = button3Action
        };
        WaitingMyMsgBox.Add(converter);
        if (ModBase.RunInUi())
            // 若为 UI 线程，立即执行弹窗刻， 避免快速（连点器）点击时多次弹窗
            MyMsgBoxTick();
        if (button2.Length > 0 || forceWait)
        {
            // 若有多个按钮则开始等待
            if (frmMain is null || (frmMain.PanMsg is null && ModBase.RunInUi()))
            {
                // 主窗体尚未加载，用老土的弹窗来替代
                WaitingMyMsgBox.Remove(converter);
                if (button2.Length > 0)
                {
                    var rawResult = Interaction.MsgBox(caption,
                        (MsgBoxStyle)((int)(button3.Length > 0 ? MsgBoxStyle.YesNoCancel : MsgBoxStyle.YesNo) +
                                      (int)(isWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), title);
                    switch (rawResult)
                    {
                        case MsgBoxResult.Yes:
                        {
                            converter.Result = 1;
                            break;
                        }
                        case MsgBoxResult.No:
                        {
                            converter.Result = 2;
                            break;
                        }
                        case MsgBoxResult.Cancel:
                        {
                            converter.Result = 3;
                            break;
                        }
                    }
                }
                else
                {
                    Interaction.MsgBox(caption,
                        (MsgBoxStyle)((int)MsgBoxStyle.OkOnly +
                                      (int)(isWarn ? MsgBoxStyle.Critical : MsgBoxStyle.Question)), title);
                    converter.Result = 1;
                }

                ModBase.Log("[Control] 主窗体加载完成前出现意料外的等待弹窗：" + button1 + "," + button2 + "," + button3,
                    ModBase.LogLevel.Debug);
            }
            else
            {
                try
                {
                    frmMain.DragStop();
                    ComponentDispatcher.PushModal();
                    Dispatcher.PushFrame(converter.WaitFrame);
                }
                finally
                {
                    ComponentDispatcher.PopModal();
                }
            }

            ModBase.Log($"[Control] 普通弹框返回：{converter.Result ?? "null"}");
            return (int)converter.Result;
        }

        // 不进行等待，直接返回
        return 1;
    }

    /// <summary>
    ///     显示输入框并返回输入的文本。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="validateRules">文本框的输入检测。</param>
    /// <param name="text">弹窗的介绍文本。</param>
    /// <param name="defaultInput">文本框的默认内容。</param>
    /// <param name="hintText">文本框的提示内容。</param>
    /// <param name="button1">显示的第一个按钮，默认为"确定"。</param>
    /// <param name="button2">显示的第二个按钮，默认为"取消"。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static string MyMsgBoxInput(string title, string text = "", string defaultInput = "",
        Collection<IValidator<string>>? validateRules = null, string hintText = "", string? button1 = null,
        string? button2 = null, bool isWarn = false)
    {
        button1 ??= GetDefaultConfirmText();
        button2 ??= GetDefaultCancelText();
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            Text = text, HintText = hintText, Type = MyMsgBoxType.Input,
            ValidateRules = validateRules ?? [], Button1 = button1, Button2 = button2,
            Content = defaultInput, IsWarn = isWarn, Title = title
        };
        WaitingMyMsgBox.Add(converter);
        // 虽然我也不知道这是啥但是能用就成了 :)
        try
        {
            frmMain?.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(converter.WaitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        ModBase.Log($"[Control] 输入弹框返回：{converter.Result}");
        return converter.Result?.ToString();
    }

    /// <summary>
    ///     显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="button1">显示的第一个按钮，默认为 "确定"。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int? MyMsgBoxSelect(List<IMyRadio> selections, string? title = null, string? button1 = null,
        string? button2 = "", bool isWarn = false)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        // 将弹窗列入队列
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Select, Button1 = button1, Button2 = button2, Content = selections, IsWarn = isWarn,
            Title = title
        };
        WaitingMyMsgBox.Add(converter);
        // 虽然我也不知道这是啥但是能用就成了 :)
        try
        {
            if (frmMain is not null)
                frmMain.DragStop();
            ComponentDispatcher.PushModal();
            Dispatcher.PushFrame(converter.WaitFrame);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        ModBase.Log($"[Control] 选择弹框返回：{converter.Result ?? "null"}");
        return (int?)converter.Result;
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
                var converter = WaitingMyMsgBox[0];
                var dialog = CreateDialogFromConverter(converter);
                if (dialog is not null)
                    frmMain.PanMsg.Children.Add(dialog);

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

    private static DialogControl? CreateDialogFromConverter(MyMsgBoxConverter conv)
    {
        switch (conv.Type)
        {
            case MyMsgBoxType.Input:
                return CreateInputDialog(conv);
            case MyMsgBoxType.Select:
                return CreateSelectDialog(conv);
            case MyMsgBoxType.Login:
                // MyMsgLogin manages its own chrome + lifecycle
                frmMain?.PanMsg.Children.Add(new MyMsgLogin(conv));
                return null;
            case MyMsgBoxType.Markdown:
            {
                var mdViewer = new Markdig.Wpf.MarkdownViewer { Markdown = conv.Text };
                return CreateStandardDialog(conv, mdViewer);
            }
            default:
            {
                var content = conv.Content as UIElement
                    ?? new TextBlock { Text = conv.Text, TextWrapping = TextWrapping.Wrap, FontSize = 15 };
                return CreateStandardDialog(conv, content);
            }
        }
    }

    private static DialogControl CreateStandardDialog(MyMsgBoxConverter conv, UIElement content)
    {
        var dialog = new DialogControl
        {
            Title = conv.Title,
            IsWarn = conv.IsWarn,
            DialogContent = content,
        };
        dialog.AddButton(conv.Button1, conv.Button1Action, isPrimary: true, id: conv.ButtonIds[0]);
        if (!string.IsNullOrEmpty(conv.Button2))
            dialog.AddButton(conv.Button2, conv.Button2Action, id: conv.ButtonIds[1]);
        if (!string.IsNullOrEmpty(conv.Button3))
            dialog.AddButton(conv.Button3, conv.Button3Action, id: conv.ButtonIds[2]);
        conv.WaitFrame = dialog.WaitFrame;
        dialog.OnClosed += result =>
        {
            conv.Result = result;
            conv.OnCloseCallback?.Invoke(result);
        };
        return dialog;
    }

    private static DialogControl CreateInputDialog(MyMsgBoxConverter conv)
    {
        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(conv.Text))
        {
            stack.Children.Add(new TextBlock
            {
                Text = conv.Text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 7),
            });
        }

        var textBox = new MyTextBox
        {
            Text = (string)(conv.Content ?? ""),
            HintText = conv.HintText,
            ValidateRules = conv.ValidateRules ?? [],
            MinWidth = 450,
        };
        stack.Children.Add(textBox);

        var dialog = new DialogControl
        {
            Title = conv.Title,
            IsWarn = conv.IsWarn,
            DialogContent = stack,
        };

        dialog.AddButton(conv.Button1, onClick: () =>
        {
            textBox.Validate();
            if (!textBox.IsValidated) return;
            conv.Result = textBox.Text;
            dialog.Close(conv.ButtonIds[0]);
        }, isPrimary: true, id: conv.ButtonIds[0]);

        if (!string.IsNullOrEmpty(conv.Button2))
        {
            dialog.AddButton(conv.Button2, onClick: () =>
            {
                conv.Result = null;
                dialog.Close(conv.ButtonIds[1]);
            }, id: conv.ButtonIds[1]);
        }

        conv.WaitFrame = dialog.WaitFrame;
        return dialog;
    }

    private static DialogControl CreateSelectDialog(MyMsgBoxConverter conv)
    {
        var panel = new StackPanel();
        var dialog = new DialogControl
        {
            Title = conv.Title,
            IsWarn = conv.IsWarn,
            DialogContent = panel,
        };

        var b1 = dialog.AddButton(conv.Button1, onClick: () =>
        {
            // handled by extra Click below
        }, isPrimary: true, id: conv.ButtonIds[0]);
        b1.IsEnabled = false;

        if (!string.IsNullOrEmpty(conv.Button2))
        {
            dialog.AddButton(conv.Button2, onClick: () =>
            {
                conv.Result = null;
                dialog.Close(conv.ButtonIds[1]);
            }, id: conv.ButtonIds[1]);
        }

        var selectedIndex = -1;
        var index = 0;
        foreach (var raw in (IEnumerable)conv.Content!)
        {
            var item = MyVirtualizingElement.TryInit((FrameworkElement)raw);
            if (item is IMyRadio radio)
            {
                if (item is MyListItem listItem)
                {
                    listItem.Type = MyListItem.CheckType.RadioBox;
                    listItem.MinHeight = 24;
                }
                else if (item is MyRadioBox radioBox)
                {
                    radioBox.MinHeight = 24;
                }

                var currentIndex = index;
                radio.Check += (_, _) =>
                {
                    selectedIndex = currentIndex;
                    b1.IsEnabled = true;
                };
                panel.Children.Add((UIElement)radio);
                index++;
            }
        }

        // Override Btn1's dummy onClick with real logic
        b1.Click += (_, _) =>
        {
            if (selectedIndex < 0) return;
            conv.Result = selectedIndex;
            dialog.Close(conv.ButtonIds[0]);
        };

        conv.WaitFrame = dialog.WaitFrame;
        return dialog;
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

        result = MyMsgBox(message, caption, btnText1, btnText2, btnText3, isWarn, forceWait: block,
            button1Action: btnAct1, button2Action: btnAct2, button3Action: btnAct3);
    }

    public static void Dialog_OnShow(DialogContext context)
    {
        if (frmMain is null)
        {
            Interaction.MsgBox(context.Caption, MsgBoxStyle.OkOnly, context.Title);
            context.Result = 1;
            context.OnClosed?.Invoke(1);
            return;
        }

        if (frmMain.Dispatcher.CheckAccess())
            ShowDialogOnUi(context);
        else
            frmMain.Dispatcher.Invoke(() => ShowDialogOnUi(context));
    }

    private static void ShowDialogOnUi(DialogContext context)
    {
        if (frmMain?.PanMsg is null) return;

        var isWarn = context.Theme == DialogTheme.Warning || context.Theme == DialogTheme.Error;

        var content = context.Content;
        if (content is null && !string.IsNullOrEmpty(context.Caption))
        {
            content = new TextBlock
            {
                Text = context.Caption,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
            };
        }

        var dialog = new DialogControl
        {
            Title = context.Title,
            IsWarn = isWarn,
            DialogContent = content as UIElement,
        };

        for (var i = 0; i < context.Buttons.Count && i < 3; i++)
        {
            var btn = context.Buttons[i];
            var isPrimary = i == 0 || btn.IsPrimary;
            var id = btn.Id > 0 ? btn.Id : i + 1;
            dialog.AddButton(btn.Text, btn.OnClick, isPrimary, id);
        }

        frmMain.PanMsg.Children.Add(dialog);

        if (context.Block)
        {
            try
            {
                frmMain.DragStop();
                ComponentDispatcher.PushModal();
                Dispatcher.PushFrame(dialog.WaitFrame);
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }
            context.Result = dialog.Result;
            context.OnClosed?.Invoke(context.Result);
        }
        else
        {
            dialog.OnClosed += result =>
            {
                context.Result = result;
                context.OnClosed?.Invoke(result);
            };
        }
    }

    #endregion
}
