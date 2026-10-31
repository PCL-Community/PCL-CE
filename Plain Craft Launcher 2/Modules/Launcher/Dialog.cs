using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using CoreDialogService = PCL.Core.UI.Dialogs.DialogService;

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
        if (frmMain is null)
        {
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步验证服务器弹窗",
                Common.LogLevel.Debug);
            return string.Empty;
        }

        var result = await CoreDialogService.Instance.ShowAsync(new AuthServerDialogRequest
        {
            Title = title ?? Lang.Text("Launch.Account.Auth.SelectServer"),
            DefaultServer = defaultServer,
            Presets = presets,
            Buttons = DialogButtons.From(
                DialogButton.Create(GetDefaultConfirmText()),
                DialogButton.Create(GetDefaultCancelText())),
            HighLight = false,
            ForceWait = false
        });
        return result.IsConfirmed ? result.InputText ?? string.Empty : string.Empty;
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
        if (frmMain is null)
        {
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步等待弹窗："
                + $"{button1},{button2},{button3}",
                Common.LogLevel.Debug);
            return 1;
        }

        var result = await CoreDialogService.Instance.ShowAsync(new TextDialogRequest
        {
            Title = title,
            Content = caption,
            Buttons = BuildButtons(button1, button2, button3),
            IsWarn = isWarn,
            HighLight = highLight,
            ForceWait = false
        });
        return result.Button;
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
        if (frmMain is null)
        {
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步输入弹窗："
                + $"{button1},{button2}",
                Common.LogLevel.Debug);
            return null;
        }

        var result = await CoreDialogService.Instance.ShowAsync(new InputDialogRequest
        {
            Title = title,
            Content = text,
            DefaultValue = defaultInput,
            HintText = hintText,
            ValidateRules = validateRules ?? [],
            Buttons = BuildButtons(button1, button2, null),
            IsWarn = isWarn,
            HighLight = false,
            ForceWait = false
        });
        return result.IsConfirmed ? result.InputText : null;
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
        if (frmMain is null)
        {
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步选择弹窗："
                + $"{button1},{button2}",
                Common.LogLevel.Debug);
            return null;
        }

        var result = await CoreDialogService.Instance.ShowAsync(new SelectDialogRequest
        {
            Title = title,
            Options = ToDialogOptions(selections),
            Buttons = BuildButtons(button1, button2, null),
            IsWarn = isWarn,
            HighLight = false,
            ForceWait = false
        });
        return result.IsConfirmed ? result.SelectedIndex : null;
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
        if (frmMain is null)
        {
            Common.Log(
                "[Control] 主窗体加载完成前出现意料外的异步多选弹窗："
                + $"{button1},{button2}",
                Common.LogLevel.Debug);
            return [];
        }

        var result = await CoreDialogService.Instance.ShowAsync(new SelectDialogRequest
        {
            Title = title,
            Options = ToDialogOptions(selections),
            MultiSelect = true,
            Buttons = BuildButtons(button1, button2, null),
            IsWarn = isWarn,
            HighLight = false,
            ForceWait = false
        });
        return result.IsConfirmed ? result.SelectedIndices?.ToList() ?? [] : [];
    }

    /// <summary>
    ///     显示正版验证弹窗，弹窗的结果由请求中的回调处理，不会阻塞调用方。
    /// </summary>
    public static void ShowDeviceCodeLogin(DialogRequest request)
    {
        ShowWithoutWaiting(request);
    }

    private static string GetDefaultDialogTitle() => Lang.Text("Common.Dialog.Title");

    private static string GetDefaultConfirmText() => Lang.Text("Common.Action.Confirm");

    private static string GetDefaultCancelText() => Lang.Text("Common.Action.Cancel");

    private static DialogButtons BuildButtons(
        string? button1,
        string? button2,
        string? button3,
        Action? button1Action = null,
        Action? button2Action = null,
        Action? button3Action = null)
    {
        return DialogButtons.From(
            DialogButton.Create(button1, button1Action),
            DialogButton.Create(button2, button2Action),
            DialogButton.Create(button3, button3Action));
    }

    private static List<DialogOption> ToDialogOptions(IEnumerable<IMyRadio> selections)
    {
        return selections.Select(item => item switch
        {
            MyListItem listItem => new DialogOption
            {
                Text = listItem.Title,
                Description = listItem.Info,
                Image = listItem.Logo,
                ImageScale = listItem.LogoScale,
                IsChecked = listItem.Checked,
                Tag = listItem
            },
            MyRadioBox radioBox => new DialogOption
            {
                Text = radioBox.Text,
                IsChecked = radioBox.Checked,
                Tag = radioBox
            },
            _ => new DialogOption
            {
                Text = item.ToString() ?? "",
                Tag = item
            }
        }).ToList();
    }

    private static void ShowWithoutWaiting(DialogRequest request)
    {
        try
        {
            CoreDialogService.Instance.Show(request);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "显示弹窗失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }

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

        var buttons = BuildButtons(button1, button2, button3, button1Action, button2Action, button3Action);
        DialogRequest request = type == MyMsgBoxType.Markdown
            ? new MarkdownDialogRequest
            {
                Title = title,
                Content = caption,
                Buttons = buttons,
                IsWarn = isWarn,
                HighLight = highLight,
                ForceWait = forceWait
            }
            : new TextDialogRequest
            {
                Title = title,
                Content = caption,
                Buttons = buttons,
                IsWarn = isWarn,
                HighLight = highLight,
                ForceWait = forceWait
            };

        if (button2.Length == 0 && !forceWait)
        {
            ShowWithoutWaiting(request);
            return 1;
        }

        var result = CoreDialogService.Instance.Show(request).Result;
        Common.Log($"[Control] 普通弹框返回：{result?.Button.ToString() ?? "null"}");
        return result?.Button ?? 0;
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

        var result = CoreDialogService.Instance.Show(new InputDialogRequest
        {
            Title = title,
            Content = text,
            DefaultValue = defaultInput,
            HintText = hintText,
            ValidateRules = validateRules ?? [],
            Buttons = BuildButtons(button1, button2, null),
            IsWarn = isWarn,
            HighLight = false,
            ForceWait = true
        }).Result;

        Common.Log($"[Control] 输入弹框返回：{result?.InputText ?? "null"}");
        return result?.InputText ?? string.Empty;
    }

    public static string MyMsgBoxAuthServer(
        string defaultServer,
        IReadOnlyDictionary<string, string> presets,
        string? title = null)
    {
        var result = CoreDialogService.Instance.Show(new AuthServerDialogRequest
        {
            Title = title ?? Lang.Text("Launch.Account.Auth.SelectServer"),
            DefaultServer = defaultServer,
            Presets = presets,
            Buttons = BuildButtons(
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Cancel"),
                null),
            HighLight = false,
            ForceWait = true
        }).Result;

        return result?.InputText ?? string.Empty;
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

        var result = CoreDialogService.Instance.Show(new SelectDialogRequest
        {
            Title = title,
            Options = ToDialogOptions(selections),
            Buttons = BuildButtons(button1, button2, null),
            IsWarn = isWarn,
            HighLight = false,
            ForceWait = true
        }).Result;

        Common.Log($"[Control] 选择弹框返回：{result?.SelectedIndex?.ToString() ?? "null"}");
        return result?.SelectedIndex;
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

        var result = CoreDialogService.Instance.Show(new SelectDialogRequest
        {
            Title = title,
            Options = ToDialogOptions(selections),
            MultiSelect = true,
            Buttons = BuildButtons(button1, button2, null),
            IsWarn = isWarn,
            HighLight = false,
            ForceWait = true
        }).Result;

        var indices = result?.SelectedIndices;
        Common.Log($"[Control] 多选弹框返回：{(indices is null ? "null" : string.Join(",", indices))}");
        return indices?.ToList();
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

        var buttonText1 = buttonList.Length > 0 && !string.IsNullOrEmpty(buttonList[0].Context)
            ? buttonList[0].Context
            : Lang.Text("Common.Action.Confirm");
        var buttonText2 = buttonList.Length > 1 && !string.IsNullOrEmpty(buttonList[1].Context)
            ? buttonList[1].Context
            : Lang.Text("Common.Action.Cancel");
        var buttonText3 = buttonList.Length > 2 ? buttonList[2].Context : "";

        var buttonAction1 = buttonList.Length > 0 ? buttonList[0].OnClick : null;
        var buttonAction2 = buttonList.Length > 1 ? buttonList[1].OnClick : null;
        var buttonAction3 = buttonList.Length > 2 ? buttonList[2].OnClick : null;

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
}
