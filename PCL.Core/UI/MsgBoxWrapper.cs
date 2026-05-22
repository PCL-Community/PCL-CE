using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.App.Localization;

namespace PCL.Core.UI;

public record MsgBoxButtonInfo(
    string Context,
    int Value = 0,
    Action? OnClick = null
);

public enum MsgBoxTheme
{
    Info,
    Warning,
    Error
}

public delegate void MsgBoxHandler(
    string message,
    string caption,
    ICollection<MsgBoxButtonInfo> buttons,
    MsgBoxTheme theme,
    bool block,
    ref int result
);

public static class MsgBoxWrapper
{
    public static event MsgBoxHandler? OnShow;

    public static int ShowWithCustomButtons(
        string message,
        string caption,
        MsgBoxTheme theme,
        bool block,
        ICollection<MsgBoxButtonInfo> buttonCollection)
    {
        var result = 0;
        if (buttonCollection.Count == 0) buttonCollection = [new MsgBoxButtonInfo(Lang.Text("Common.Action.Confirm"))];
        OnShow?.Invoke(message, caption, buttonCollection, theme, block, ref result);
        return result;
    }

    public static int ShowWithCustomButtons(
        string message,
        string? caption = null,
        MsgBoxTheme theme = MsgBoxTheme.Info,
        bool block = true,
        params MsgBoxButtonInfo[] buttons)
    {
        return ShowWithCustomButtons(message, caption ?? Lang.Text("Common.Dialog.Title"), theme, block, buttonCollection: buttons);
    }

    public static int Show(
        string message,
        string? caption = null,
        MsgBoxTheme theme = MsgBoxTheme.Info,
        bool block = true,
        params string[] buttons)
    {
        var index = 0;
        var list = buttons.Select(button => new MsgBoxButtonInfo(button, ++index)).ToList();
        return ShowWithCustomButtons(message, caption ?? Lang.Text("Common.Dialog.Title"), theme, block, list);
    }
}
