using System;

namespace PCL.Core.UI.MsgBox;

public record MsgBoxButtonInfo(
    string Text,
    int Value = 0,
    Action? OnClick = null
    );