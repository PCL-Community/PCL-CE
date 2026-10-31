using System;

namespace PCL.Core.UI.Dialogs;

/// <summary>弹窗按钮。文本为空表示该位置没有按钮。</summary>
public readonly record struct DialogButton(string? Text, Action? OnClick = null)
{
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    public static DialogButton None => default;

    public static DialogButton Create(string? text, Action? onClick = null) => new(text, onClick);
}
