// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;

namespace PCL.Desktop.Controls.Legacy;

public class MyTextButton : Button
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyTextButton, string>(nameof(Text), string.Empty);

    public MyTextButton()
    {
        this.GetObservable(TextProperty).Subscribe(text => Content = text);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
