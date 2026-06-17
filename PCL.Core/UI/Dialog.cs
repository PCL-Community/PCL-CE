using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PCL.Core.App.Localization;

namespace PCL.Core.UI;

/// <summary>
///     Standard dialog result IDs for button matching.
/// </summary>
public static class DialogResult
{
    public const int Ok = 1;
    public const int Cancel = 2;
    public const int Yes = 6;
    public const int No = 7;
}

public class DialogButton
{
    public string Text { get; set; }
    public int Id { get; set; }
    public Action? OnClick { get; set; }
    public bool IsPrimary { get; set; }

    public DialogButton(string text, Action? onClick = null, bool isPrimary = false, int id = 0)
    {
        Text = text;
        OnClick = onClick;
        IsPrimary = isPrimary;
        Id = id;
    }

    // -- presets --

    public static DialogButton Confirm(string? text = null)
        => new(text ?? Lang.Text("Common.Action.Confirm"), isPrimary: true, id: DialogResult.Ok);

    public static DialogButton Cancel(string? text = null)
        => new(text ?? Lang.Text("Common.Action.Cancel"), id: DialogResult.Cancel);

    public static DialogButton Yes(string? text = null)
        => new(text ?? Lang.Text("Common.Option.Yes"), isPrimary: true, id: DialogResult.Yes);

    public static DialogButton No(string? text = null)
        => new(text ?? Lang.Text("Common.Option.No"), id: DialogResult.No);
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
            Buttons = _BuildButtons(buttons),
        });
    }

    public static int Show(DialogContext context)
    {
        if (context.Buttons.Count == 0)
            context.Buttons.Add(DialogButton.Confirm());
        OnShow?.Invoke(context);
        return context.Result;
    }

    private static Collection<DialogButton> _BuildButtons(string[] buttonTexts)
    {
        var list = new Collection<DialogButton>();
        for (var i = 0; i < buttonTexts.Length; i++)
        {
            list.Add(new DialogButton(buttonTexts[i], isPrimary: i == 0, id: i + 1));
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
