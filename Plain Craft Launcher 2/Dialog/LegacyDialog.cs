using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentValidation;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static partial class ModMain
{
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

    #endregion
}
