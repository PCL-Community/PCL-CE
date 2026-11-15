using System.Collections.Generic;

namespace PCL.Core.UI.Dialogs;

/// <summary>弹窗结果。<see cref="Button"/> 为点击的按钮编号（从 1 开始），0 表示未点击按钮。</summary>
public sealed record DialogResult(
    int Button = 0,
    string? InputText = null,
    int? SelectedIndex = null,
    IReadOnlyList<int>? SelectedIndices = null)
{
    public static DialogResult None { get; } = new();

    public static DialogResult Cancel { get; } = new(2);

    public bool IsConfirmed => Button == 1;

    public bool IsCanceled => Button != 1;
}
