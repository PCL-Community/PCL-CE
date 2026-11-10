using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;

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

public static class MsgBoxWrapper
{
    public static int ShowWithCustomButtons(
        string message,
        string? caption,
        MsgBoxTheme theme,
        bool block,
        ICollection<MsgBoxButtonInfo> buttonCollection)
    {
        var buttons = buttonCollection.Count == 0
            ? [new MsgBoxButtonInfo(Lang.Text("Common.Action.Confirm"))]
            : buttonCollection.ToList();

        var request = new TextDialogRequest
        {
            Title = _ResolveCaption(caption, theme),
            Content = message,
            Buttons = DialogButtons.From(
                buttons.Select(button => DialogButton.Create(button.Context, button.OnClick)).ToArray()),
            IsWarn = theme is not MsgBoxTheme.Info,
            ForceWait = block
        };

        if (!block && buttons.Count == 1)
        {
            _ = DialogService.Instance.ShowAsync(request);
            return 1;
        }

        return DialogService.Instance.Show(request).Result?.Button ?? 0;
    }

    public static int ShowWithCustomButtons(
        string message,
        string? caption = null,
        MsgBoxTheme theme = MsgBoxTheme.Info,
        bool block = true,
        params MsgBoxButtonInfo[] buttons)
    {
        return ShowWithCustomButtons(message, caption, theme, block, buttonCollection: buttons);
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
        return ShowWithCustomButtons(message, caption, theme, block, list);
    }

    private static string _ResolveCaption(string? caption, MsgBoxTheme theme)
    {
        if (!string.IsNullOrWhiteSpace(caption)) return caption;

        return theme switch
        {
            MsgBoxTheme.Warning => Lang.Text("Common.Dialog.Warning"),
            MsgBoxTheme.Error => Lang.Text("SystemDialog.Error.Title"),
            _ => Lang.Text("Common.Dialog.Title")
        };
    }
}
