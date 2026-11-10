using PCL.Core.App.Localization;
using System;
using System.Collections.Generic;

namespace PCL.Core.UI.Dialogs;

/// <summary>固定三个位置的弹窗按钮组。</summary>
public readonly struct DialogButtons(DialogButton first, DialogButton second = default, DialogButton third = default)
{
    public const int Capacity = 3;

    public DialogButton this[int index] => index switch
    {
        0 => first,
        1 => second,
        2 => third,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    /// <summary>按钮数量，从第一个空位开始计算。</summary>
    public int Length
    {
        get
        {
            if (first.IsEmpty) return 0;
            if (second.IsEmpty) return 1;
            return third.IsEmpty ? 2 : 3;
        }
    }

    public bool IsEmpty => first.IsEmpty;

    public DialogButton[] ToArray() => [first, second, third];

    public static DialogButtons From(params DialogButton[] buttons)
    {
        if (buttons.Length > Capacity)
            throw new ArgumentException($"弹窗最多支持 {Capacity} 个按钮。", nameof(buttons));

        return new DialogButtons(
            buttons.Length > 0 ? buttons[0] : default,
            buttons.Length > 1 ? buttons[1] : default,
            buttons.Length > 2 ? buttons[2] : default);
    }

    public static DialogButtons From(params string[] texts) => From(Array.ConvertAll(texts, text => DialogButton.Create(text)));

    /// <summary>按当前语言生成按钮组。</summary>
    public static DialogButtons Create(params string[] textKeys)
    {
        var buttons = new DialogButton[textKeys.Length];
        for (var i = 0; i < textKeys.Length; i++)
            buttons[i] = DialogButton.Create(Lang.Text(textKeys[i]));

        return From(buttons);
    }

    public static DialogButtons Ok => Create("Common.Action.Confirm");

    public static DialogButtons OkCancel => Create("Common.Action.Confirm", "Common.Action.Cancel");

    public static DialogButtons ConfirmCancel => Create("Common.Action.Confirm", "Common.Action.Cancel");

    public static DialogButtons ContinueCancel => Create("Common.Action.Continue", "Common.Action.Cancel");

    public static DialogButtons AgreeDecline => Create("Common.Action.Agree", "Common.Action.Decline");

    public static DialogButtons GotIt => Create("Common.Action.GotIt");

    public static DialogButtons Close => Create("Common.Action.Close");

    public static DialogButtons OkCancelWithAction(Action? onOk, Action? onCancel = null)
        => new(
            DialogButton.Create(Lang.Text("Common.Action.Confirm"), onOk),
            DialogButton.Create(Lang.Text("Common.Action.Cancel"), onCancel));

    public static DialogButtons FromKeysWithActions(IReadOnlyList<(string Key, Action? OnClick)> buttons)
    {
        var list = new DialogButton[buttons.Count];
        for (var i = 0; i < buttons.Count; i++)
            list[i] = DialogButton.Create(Lang.Text(buttons[i].Key), buttons[i].OnClick);

        return From(list);
    }
}
