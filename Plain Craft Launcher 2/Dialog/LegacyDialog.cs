using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using FluentValidation;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static partial class ModMain
{
    /// <summary>
    ///     等待显示的弹窗。
    /// </summary>
    public static List<MyMsgBoxConverter> WaitingMyMsgBox { get; } = [];

    public class MyMsgBoxConverter
    {
        public object AuthUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        public string Button1 = "";
        public Action? Button1Action;
        public string Button2 = "";
        public Action? Button2Action;
        public string Button3 = "";
        public Action? Button3Action;
        public object? Content;
        public bool ForceWait;
        public bool HighLight;
        public string HintText = "";
        public bool IsExited;
        public bool IsWarn;
        public object? Result;
        public string? Text;
        public string? Title;
        public MyMsgBoxType Type;
        public Collection<IValidator<string>>? ValidateRules;
        public DispatcherFrame WaitFrame = new(true);
        public int[] ButtonIds = [1, 2, 3];
        public Action<int>? OnCloseCallback;
    }

    public enum MyMsgBoxType
    {
        Text, Select, Input, Login, Markdown
    }

    #region Legacy wrappers — delegate to DialogManager

    public static int MyMsgBox(string caption, string? title = null, string? button1 = null, string? button2 = "",
        string? button3 = "", bool isWarn = false, bool highLight = true, bool forceWait = false,
        Action? button1Action = null, Action? button2Action = null, Action? button3Action = null)
    {
        var mgr = DialogManager.Instance;
        if (mgr is null) return 1;

        var buttons = new List<string>();
        if (!string.IsNullOrEmpty(button1)) buttons.Add(button1!);
        if (!string.IsNullOrEmpty(button2)) buttons.Add(button2!);
        if (!string.IsNullOrEmpty(button3)) buttons.Add(button3!);
        if (buttons.Count == 0) buttons.Add(Lang.Text("Common.Action.Confirm"));

        return mgr.ShowText(caption, title,
            isWarn ? DialogTheme.Warning : DialogTheme.Info,
            forceWait || buttons.Count > 1,
            buttons.ToArray());
    }

    public static int MyMsgBoxMarkdown(string caption, string? title = null, string? button1 = null, string? button2 = "",
        string? button3 = "", bool isWarn = false, bool highLight = true, bool forceWait = false,
        Action? button1Action = null, Action? button2Action = null, Action? button3Action = null)
    {
        var mgr = DialogManager.Instance;
        if (mgr is null) return 1;

        var buttons = new List<string>();
        if (!string.IsNullOrEmpty(button1)) buttons.Add(button1!);
        if (!string.IsNullOrEmpty(button2)) buttons.Add(button2!);
        if (!string.IsNullOrEmpty(button3)) buttons.Add(button3!);
        if (buttons.Count == 0) buttons.Add(Lang.Text("Common.Action.Confirm"));

        return mgr.ShowMarkdown(caption, title,
            isWarn ? DialogTheme.Warning : DialogTheme.Info,
            forceWait || buttons.Count > 1,
            buttons.ToArray());
    }

    public static string MyMsgBoxInput(string title, string text = "", string defaultInput = "",
        Collection<IValidator<string>>? validateRules = null, string hintText = "", string? button1 = null,
        string? button2 = null, bool isWarn = false)
    {
        return DialogManager.Instance?.ShowInput(title, text, defaultInput,
            validateRules, hintText, button1, button2, isWarn) ?? "";
    }

    public static int? MyMsgBoxSelect(List<IMyRadio> selections, string? title = null, string? button1 = null,
        string? button2 = "", bool isWarn = false)
    {
        return DialogManager.Instance?.ShowSelect(selections, title, button1, button2, isWarn);
    }

    public static void MsgBoxWrapper_OnShow(string message, string caption, ICollection<MsgBoxButtonInfo> buttons,
        MsgBoxTheme theme, bool block, ref int result)
    {
        result = DialogManager.Instance?.ShowLegacy(message, caption, buttons, theme, block) ?? 1;
    }

    public static void Dialog_OnShow(DialogContext context)
    {
        // Forward to DialogManager for backward compat with PCL.Core.UI.Dialog.OnShow event
        if (DialogManager.Instance is null) return;
        if (context.Block)
            DialogManager.Instance.Show(context);
        else
            _ = DialogManager.Instance.ShowAsync(context);
    }

    #endregion
}
