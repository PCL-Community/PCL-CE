using System;
using PCL.Core.App.Localization;

namespace PCL.Core.UI;

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

    public static DialogButton Confirm(string? text = null)
        => new(text ?? Lang.Text("Common.Action.Confirm"), isPrimary: true, id: DialogResult.Ok);

    public static DialogButton Cancel(string? text = null)
        => new(text ?? Lang.Text("Common.Action.Cancel"), id: DialogResult.Cancel);

    public static DialogButton Yes(string? text = null)
        => new(text ?? Lang.Text("Common.Option.Yes"), isPrimary: true, id: DialogResult.Yes);

    public static DialogButton No(string? text = null)
        => new(text ?? Lang.Text("Common.Option.No"), id: DialogResult.No);
}
