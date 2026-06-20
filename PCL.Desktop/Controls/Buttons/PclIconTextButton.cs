// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;

namespace PCL.Desktop.Controls.Buttons;

public class PclIconTextButton : PclButton
{
    public static readonly StyledProperty<string?> IconKeyProperty =
        AvaloniaProperty.Register<PclIconTextButton, string?>(
            nameof(IconKey));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PclIconTextButton, string?>(
            nameof(Text));

    public string? IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
