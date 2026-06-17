using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PCL.Core.App.Localization;

namespace PCL.Core.UI;

[Obsolete("Use PCL.Core.UI.Dialog instead")]
public record MsgBoxButtonInfo(
    string Context,
    int Value = 0,
    Action? OnClick = null
);

[Obsolete("Use PCL.Core.UI.DialogTheme instead")]
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

[Obsolete("Use PCL.Core.UI.Dialog instead")]
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
        var buttons = new Collection<DialogButton>();
        foreach (var btn in buttonCollection)
        {
            buttons.Add(new DialogButton(btn.Context, btn.OnClick));
        }
        var result = Dialog.Show(new DialogContext
        {
            Caption = message,
            Title = caption,
            Theme = (DialogTheme)(int)theme,
            Block = block,
            Content = null,
            Buttons = buttons,
        });
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
