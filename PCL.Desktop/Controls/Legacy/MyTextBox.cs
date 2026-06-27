// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;

namespace PCL.Desktop.Controls.Legacy;

public class MyTextBox : TextBox
{
    public static readonly StyledProperty<bool> HasBackgroundProperty =
        AvaloniaProperty.Register<MyTextBox, bool>(nameof(HasBackground), true);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MyTextBox, string>(nameof(HintText), string.Empty);

    public MyTextBox()
    {
        this.GetObservable(HintTextProperty).Subscribe(hint => PlaceholderText = hint);
    }

    public bool HasBackground
    {
        get => GetValue(HasBackgroundProperty);
        set => SetValue(HasBackgroundProperty, value);
    }

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }
}
