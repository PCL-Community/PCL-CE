using System;
using System.Collections.ObjectModel;

namespace PCL.Core.UI;

public class DialogContext
{
    public string Caption { get; set; } = "";
    public string Title { get; set; } = "";
    public DialogTheme Theme { get; set; } = DialogTheme.Info;
    public bool Block { get; set; } = true;
    public bool ShowTitle { get; set; } = true;
    public object? Content { get; set; }
    public Collection<DialogButton> Buttons { get; set; } = [];
    public int Result { get; set; }
    public Action<int>? OnClosed { get; set; }
    internal DialogControl? _dialog;
}
