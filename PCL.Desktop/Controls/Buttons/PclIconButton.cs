// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Automation;

namespace PCL.Desktop.Controls.Buttons;

public class PclIconButton : PclButton
{
    public static readonly StyledProperty<string?> IconKeyProperty =
        AvaloniaProperty.Register<PclIconButton, string?>(
            nameof(IconKey));

    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PclIconButton, string?>(
            nameof(AccessibleName));

    static PclIconButton()
    {
        AccessibleNameProperty.Changed.AddClassHandler<PclIconButton>(
            static (button, args) =>
                AutomationProperties.SetName(
                    button,
                    args.NewValue as string));
    }

    public string? IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public string? AccessibleName
    {
        get => GetValue(AccessibleNameProperty);
        set => SetValue(AccessibleNameProperty, value);
    }
}
