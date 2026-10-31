using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.UI;

namespace PCL;

public static partial class Launcher
{
    public enum MyMsgBoxType
    {
        Text,
        Select,
        Input,
        AuthServer,
        Login,
        Markdown
    }

    /// <summary>
    ///     显示弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="caption">弹窗的内容。</param>
    /// <param name="button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="button3">显示的第三个按钮，默认为空。</param>
    /// <param name="button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int MyMsgBox(
        string caption,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool highLight = true,
        bool forceWait = false,
        Action? button1Action = null,
        Action? button2Action = null,
        Action? button3Action = null)
    {
        return ShowTextDialog(
            caption,
            title,
            button1,
            button2,
            button3,
            isWarn,
            highLight,
            forceWait,
            button1Action,
            button2Action,
            button3Action,
            MyMsgBoxType.Text);
    }

    /// <summary>
    ///     显示弹窗，返回点击按钮的编号（从 1 开始）。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="caption">弹窗的内容。</param>
    /// <param name="button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="button3">显示的第三个按钮，默认为空。</param>
    /// <param name="button1Action">点击第一个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button2Action">点击第二个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="button3Action">点击第三个按钮将执行该方法，不关闭弹窗。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int MyMsgBoxMarkdown(
        string caption,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool highLight = true,
        bool forceWait = false,
        Action? button1Action = null,
        Action? button2Action = null,
        Action? button3Action = null)
    {
        return ShowTextDialog(
            caption,
            title,
            button1,
            button2,
            button3,
            isWarn,
            highLight,
            forceWait,
            button1Action,
            button2Action,
            button3Action,
            MyMsgBoxType.Markdown);
    }

    public static async Task<string> MyMsgBoxAuthServerAsync(
        string defaultServer,
        IReadOnlyDictionary<string, string> presets,
        string? title = null)
    {
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.AuthServer,
            Title = title ?? Lang.Text("Launch.Account.Auth.SelectServer"),
            Button1 = GetDefaultConfirmText(),
            Button2 = GetDefaultCancelText(),
            AuthServerDefault = defaultServer,
            AuthServerPresets = presets,
            ForceWait = true
        };
        WaitingMyMsgBox.Add(converter);
        if (Common.RunInUi())
            MyMsgBoxTick();
        if (frmMain is null)
        {
            WaitingMyMsgBox.Remove(converter);
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步验证服务器弹窗",
                Common.LogLevel.Debug);
            return string.Empty;
        }

        return (await converter.CompletionObject.Task as string) ?? string.Empty;
    }

    public static async Task<int> MyMsgBoxAsync(
        string caption,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool highLight = true)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        button3 ??= "";
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Text,
            Button1 = button1,
            Button2 = button2,
            Button3 = button3,
            Text = caption,
            IsWarn = isWarn,
            Title = title,
            HighLight = highLight,
            ForceWait = true
        };
        WaitingMyMsgBox.Add(converter);
        if (Common.RunInUi())
            MyMsgBoxTick();
        if (frmMain is null)
        {
            WaitingMyMsgBox.Remove(converter);
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步等待弹窗："
                + $"{button1},{button2},{button3}",
                Common.LogLevel.Debug);
            return 1;
        }

        return await converter.Completion.Task ?? 0;
    }

    public static async Task<string?> MyMsgBoxInputAsync(
        string title,
        string text = "",
        string defaultInput = "",
        Collection<IValidator<string>>? validateRules = null,
        string hintText = "",
        string? button1 = null,
        string? button2 = null,
        bool isWarn = false)
    {
        button1 ??= GetDefaultConfirmText();
        button2 ??= GetDefaultCancelText();
        var converter = new MyMsgBoxConverter
        {
            Text = text,
            HintText = hintText,
            Type = MyMsgBoxType.Input,
            ValidateRules = validateRules ?? [],
            Button1 = button1,
            Button2 = button2,
            Content = defaultInput,
            IsWarn = isWarn,
            Title = title,
            ForceWait = true
        };
        WaitingMyMsgBox.Add(converter);
        if (Common.RunInUi())
            MyMsgBoxTick();
        if (frmMain is null)
        {
            WaitingMyMsgBox.Remove(converter);
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步输入弹窗："
                + $"{button1},{button2}",
                Common.LogLevel.Debug);
            return null;
        }

        return await converter.CompletionObject.Task as string;
    }

    public static async Task<int?> MyMsgBoxSelectAsync(
        List<IMyRadio> selections,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        bool isWarn = false)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Select,
            Button1 = button1,
            Button2 = button2,
            Content = selections,
            IsWarn = isWarn,
            Title = title,
            ForceWait = true
        };
        WaitingMyMsgBox.Add(converter);
        if (Common.RunInUi())
            MyMsgBoxTick();
        if (frmMain is null)
        {
            WaitingMyMsgBox.Remove(converter);
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步选择弹窗："
                + $"{button1},{button2}",
                Common.LogLevel.Debug);
            return null;
        }

        return await converter.CompletionObject.Task as int?;
    }

    public static async Task<List<int>> MyMsgBoxMultiSelectAsync(
        List<MyListItem> selections,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        bool isWarn = false)
    {
        title ??= GetDefaultDialogTitle();
        button1 ??= GetDefaultConfirmText();
        button2 ??= "";
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Select,
            MultiSelect = true,
            Button1 = button1,
            Button2 = button2,
            Content = selections,
            IsWarn = isWarn,
            Title = title,
            ForceWait = true
        };
        WaitingMyMsgBox.Add(converter);
        if (Common.RunInUi())
            MyMsgBoxTick();
        if (frmMain is null)
        {
            WaitingMyMsgBox.Remove(converter);
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步多选弹窗："
                + $"{button1},{button2}",
                Common.LogLevel.Debug);
            return [];
        }

        return (await converter.CompletionObject.Task as List<int>) ?? [];
    }

    private static string GetDefaultDialogTitle() => Lang.Text("Common.Dialog.Title");

    private static string GetDefaultConfirmText() => Lang.Text("Common.Action.Confirm");

    private static string GetDefaultCancelText() => Lang.Text("Common.Action.Cancel");

    private static int ShowTextDialog(
        string caption,
        string? title,
        string? button1,
        string? button2,
        string? button3,
        bool isWarn,
        bool highLight,
        bool forceWait,
        Action? button1Action,
        Action? button2Action,
        Action? button3Action,
        MyMsgBoxType type)
    {
        title ??= Lang.Text("Common.Dialog.Title");
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= "";
        button3 ??= "";

        var converter = new MyMsgBoxConverter
        {
            Type = type,
            Button1 = button1,
            Button2 = button2,
            Button3 = button3,
            Text = caption,
            IsWarn = isWarn,
            Title = title,
            HighLight = highLight,
            ForceWait = true,
            Button1Action = button1Action,
            Button2Action = button2Action,
            Button3Action = button3Action
        };

        WaitingMyMsgBox.Add(converter);

        if (Common.RunInUi())
            MyMsgBoxTick();

        if (button2.Length == 0 && !forceWait)
            return 1;

        if (frmMain is null || (frmMain.PanMsg is null && Common.RunInUi()))
        {
            WaitingMyMsgBox.Remove(converter);
            ShowFallbackTextDialog(
                converter,
                caption,
                title,
                button1,
                button2,
                button3,
                isWarn);
        }
        else
        {
            WaitForDialog(converter);
        }

        Common.Log($"[Control] 普通弹框返回：{converter.Result ?? "null"}");
        return converter.Result is int result ? result : 0;
    }

    private static void ShowFallbackTextDialog(
        MyMsgBoxConverter converter,
        string caption,
        string title,
        string button1,
        string button2,
        string button3,
        bool isWarn)
    {
        var iconStyle = isWarn
            ? MsgBoxStyle.Critical
            : MsgBoxStyle.Question;

        if (button2.Length > 0)
        {
            var buttonStyle = button3.Length > 0
                ? MsgBoxStyle.YesNoCancel
                : MsgBoxStyle.YesNo;

            var rawResult = Interaction.MsgBox(
                caption,
                (MsgBoxStyle)((int)buttonStyle + (int)iconStyle),
                title);

            converter.Result = rawResult switch
            {
                MsgBoxResult.Yes => 1,
                MsgBoxResult.No => 2,
                MsgBoxResult.Cancel => 3,
                _ => converter.Result
            };
        }
        else
        {
            Interaction.MsgBox(
                caption,
                (MsgBoxStyle)((int)MsgBoxStyle.OkOnly + (int)iconStyle),
                title);

            converter.Result = 1;
        }

        Common.Log(
            $"[Control] 主窗体加载完成前出现意料外的等待弹窗：{button1},{button2},{button3}",
            Common.LogLevel.Debug);
    }

    private static void WaitForDialog(MyMsgBoxConverter converter)
    {
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
    }

    /// <summary>
    ///     显示输入框并返回输入的文本。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="validateRules">文本框的输入检测。</param>
    /// <param name="text">弹窗的介绍文本。</param>
    /// <param name="defaultInput">文本框的默认内容。</param>
    /// <param name="hintText">文本框的提示内容。</param>
    /// <param name="button1">显示的第一个按钮，默认为“确定”。</param>
    /// <param name="button2">显示的第二个按钮，默认为“取消”。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static string MyMsgBoxInput(
        string title,
        string text = "",
        string defaultInput = "",
        Collection<IValidator<string>>? validateRules = null,
        string hintText = "",
        string? button1 = null,
        string? button2 = null,
        bool isWarn = false)
    {
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= Lang.Text("Common.Action.Cancel");

        var converter = new MyMsgBoxConverter
        {
            Text = text,
            HintText = hintText,
            Type = MyMsgBoxType.Input,
            ValidateRules = validateRules ?? [],
            Button1 = button1,
            Button2 = button2,
            Content = defaultInput,
            IsWarn = isWarn,
            Title = title
        };

        WaitingMyMsgBox.Add(converter);
        WaitForDialog(converter);

        Common.Log($"[Control] 输入弹框返回：{converter.Result}");
        return converter.Result?.ToString() ?? string.Empty;
    }

    public static string MyMsgBoxAuthServer(
        string defaultServer,
        IReadOnlyDictionary<string, string> presets,
        string? title = null)
    {
        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.AuthServer,
            Title = title ?? Lang.Text("Launch.Account.Auth.SelectServer"),
            Button1 = Lang.Text("Common.Action.Confirm"),
            Button2 = Lang.Text("Common.Action.Cancel"),
            AuthServerDefault = defaultServer,
            AuthServerPresets = presets,
            ForceWait = true
        };

        WaitingMyMsgBox.Add(converter);
        WaitForDialog(converter);

        return converter.Result?.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     显示选择框并返回选择的第几项（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="button1">显示的第一个按钮，默认为 “确定”。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static int? MyMsgBoxSelect(
        List<IMyRadio> selections,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        bool isWarn = false)
    {
        title ??= Lang.Text("Common.Dialog.Title");
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= "";

        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Select,
            Button1 = button1,
            Button2 = button2,
            Content = selections,
            IsWarn = isWarn,
            Title = title
        };

        WaitingMyMsgBox.Add(converter);
        WaitForDialog(converter);

        Common.Log($"[Control] 选择弹框返回：{converter.Result ?? "null"}");
        return (int?)converter.Result;
    }

    /// <summary>
    ///     显示多选选择框并返回勾选的所有项索引（从 0 开始）。若点击第二个按钮，则返回 Nothing。
    /// </summary>
    /// <param name="selections">需要展示的可勾选列表项。</param>
    /// <param name="title">弹窗的标题。</param>
    /// <param name="button1">显示的第一个按钮，默认为 “确定”。</param>
    /// <param name="button2">显示的第二个按钮，默认为空。</param>
    /// <param name="isWarn">是否为警告弹窗，若为 True，弹窗配色和背景会变为红色。</param>
    public static List<int>? MyMsgBoxMultiSelect(
        List<MyListItem> selections,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        bool isWarn = false)
    {
        title ??= Lang.Text("Common.Dialog.Title");
        button1 ??= Lang.Text("Common.Action.Confirm");
        button2 ??= "";

        var converter = new MyMsgBoxConverter
        {
            Type = MyMsgBoxType.Select,
            MultiSelect = true,
            Button1 = button1,
            Button2 = button2,
            Content = selections,
            IsWarn = isWarn,
            Title = title
        };

        WaitingMyMsgBox.Add(converter);
        WaitForDialog(converter);

        Common.Log($"[Control] 多选弹框返回：{converter.Result ?? "null"}");
        return (List<int>?)converter.Result;
    }

    public static void MyMsgBoxTick()
    {
        try
        {
            var main = frmMain;
            if (main?.PanMsg is null || main.WindowState == WindowState.Minimized)
                return;
            // Core 弹窗服务正在显示时不再处理遗留队列
            if (PCL.Core.UI.Dialogs.DialogService.Instance.IsShowing)
                return;

            var messagePanel = main.PanMsg;
            var background = main.PanMsgBackground;

            if (messagePanel.Children.Count > 0)
            {
                background.Visibility = Visibility.Visible;
                return;
            }

            if (WaitingMyMsgBox.Count == 0)
            {
                if (background.Visibility != Visibility.Collapsed)
                    background.Visibility = Visibility.Collapsed;

                return;
            }

            background.Visibility = Visibility.Visible;

            var converter = WaitingMyMsgBox[0];
            UIElement? dialog = converter.Type switch
            {
                MyMsgBoxType.Input => new MyMsgInput(converter),
                MyMsgBoxType.Select => new MyMsgSelect(converter),
                MyMsgBoxType.AuthServer => new MyMsgAuthServer(converter),
                MyMsgBoxType.Text => new MyMsgText(converter),
                MyMsgBoxType.Login => new MyMsgLogin(converter),
                MyMsgBoxType.Markdown => new MyMsgMarkdown(converter),
                _ => null
            };

            if (dialog is not null)
                messagePanel.Children.Add(dialog);

            WaitingMyMsgBox.RemoveAt(0);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "处理等待中的弹窗失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }

    public static void MsgBoxWrapper_OnShow(
        string message,
        string caption,
        ICollection<MsgBoxButtonInfo> buttons,
        MsgBoxTheme theme,
        bool block,
        ref int result)
    {
        var buttonList = buttons.Take(3).ToArray();

        var buttonText1 = buttonList.Length < 1
            ? Lang.Text("Common.Action.Confirm")
            : buttonList[0].Context;
        var buttonAction1 = buttonList.Length < 1
            ? null
            : buttonList[0].OnClick;

        var buttonText2 = buttonList.Length < 2
            ? Lang.Text("Common.Action.Cancel")
            : buttonList[1].Context;
        var buttonAction2 = buttonList.Length < 2
            ? null
            : buttonList[1].OnClick;

        var buttonText3 = buttonList.Length < 3
            ? ""
            : buttonList[2].Context;
        var buttonAction3 = buttonList.Length < 3
            ? null
            : buttonList[2].OnClick;

        var isWarn = theme is MsgBoxTheme.Warning or MsgBoxTheme.Error;

        result = MyMsgBox(
            message,
            caption,
            buttonText1,
            buttonText2,
            buttonText3,
            isWarn,
            forceWait: block,
            button1Action: buttonAction1,
            button2Action: buttonAction2,
            button3Action: buttonAction3);
    }

    /// <summary>
    ///     存储弹窗信息的转换器。
    /// </summary>
    public class MyMsgBoxConverter
    {
        public string AuthServerDefault = "";

        public IReadOnlyDictionary<string, string>? AuthServerPresets;

        public string Button1 = "";

        /// <summary>
        ///     点击第一个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action? Button1Action;

        public string Button2 = "";

        /// <summary>
        ///     点击第二个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action? Button2Action;

        public string Button3 = "";

        /// <summary>
        ///     点击第三个按钮将执行该方法，不关闭弹窗。
        /// </summary>
        public Action? Button3Action;

        public Action<object>? CompletionHandler;

        /// <summary>
        ///     输入模式：文本框的文本。
        ///     选择模式：需要放进去的 List(Of MyListItem)。
        ///     登录模式：登录步骤 1 中返回的 JSON。
        /// </summary>
        public object? Content;

        /// <summary>
        ///     Optional provider-specific device-code polling callback used by OAuth providers other than
        ///     Microsoft.
        /// </summary>
        public Func<JsonObject, CancellationToken, Task<AuthorizeResult?>>? DeviceCodePoll;

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

        public Func<AuthorizeResult, CancellationToken, Task>? LoginResultHandler;

        /// <summary>
        ///     选择模式：是否允许勾选多个选项
        /// </summary>
        public bool MultiSelect;

        /// <summary>
        ///     输入模式：输入的文本。若点击了 非 第一个按钮，则为 Nothing。
        ///     选择模式：点击的按钮编号，从 1 开始。
        ///     登录模式：字符串数组 {AccessToken, RefreshToken} 或一个 Exception。
        /// </summary>
        public object? Result;

        public string? Text;
        public string? Title;
        public MyMsgBoxType Type;

        /// <summary>
        ///     输入模式：输入验证规则。
        /// </summary>
        public Collection<IValidator<string>>? ValidateRules;

        public readonly TaskCompletionSource<int?> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public readonly TaskCompletionSource<object?> CompletionObject =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DispatcherFrame WaitFrame = new(true);
    }
}
