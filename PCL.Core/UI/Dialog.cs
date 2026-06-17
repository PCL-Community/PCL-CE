using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PCL.Core.App.Localization;

namespace PCL.Core.UI;

public class DialogButton
{
    public string Text { get; set; }
    public Action? OnClick { get; set; }
    public bool IsPrimary { get; set; }

    public DialogButton(string text, Action? onClick = null, bool isPrimary = false)
    {
        Text = text;
        OnClick = onClick;
        IsPrimary = isPrimary;
    }
}

public static class Dialog
{
    public static event Action<DialogContext>? OnShow;

    public static int Show(string caption, string? title = null,
        DialogTheme theme = DialogTheme.Info, bool block = true,
        params string[] buttons)
    {
        return Show(new DialogContext
        {
            Caption = caption,
            Title = title ?? Lang.Text("Common.Dialog.Title"),
            Theme = theme,
            Block = block,
            Content = null,
            Buttons = BuildButtons(buttons, theme),
        });
    }

    public static int Show(DialogContext context)
    {
        if (context.Buttons.Count == 0)
            context.Buttons.Add(new DialogButton(Lang.Text("Common.Action.Confirm")));
        OnShow?.Invoke(context);
        return context.Result;
    }

    private static Collection<DialogButton> BuildButtons(string[] buttonTexts, DialogTheme theme)
    {
        var list = new Collection<DialogButton>();
        for (var i = 0; i < buttonTexts.Length; i++)
        {
            list.Add(new DialogButton(buttonTexts[i], isPrimary: i == 0));
        }
        return list;
    }
}

public enum DialogTheme
{
    Info,
    Warning,
    Error
}

public class DialogContext
{
    public string Caption { get; set; } = "";
    public string Title { get; set; } = "";
    public DialogTheme Theme { get; set; } = DialogTheme.Info;
    public bool Block { get; set; } = true;
    public object? Content { get; set; }
    public Collection<DialogButton> Buttons { get; set; } = [];
    public int Result { get; set; }
}
