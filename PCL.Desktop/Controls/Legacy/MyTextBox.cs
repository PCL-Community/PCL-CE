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

    public bool HasBackground
    {
        get => GetValue(HasBackgroundProperty);
        set => SetValue(HasBackgroundProperty, value);
    }
}
