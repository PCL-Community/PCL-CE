// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;

namespace PCL.Desktop.Controls.Legacy;

public class MyCard : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> IsSwapedProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsSwaped));

    public static readonly StyledProperty<bool> SwapLogoRightProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(SwapLogoRight));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsSwaped
    {
        get => GetValue(IsSwapedProperty);
        set => SetValue(IsSwapedProperty, value);
    }

    public bool SwapLogoRight
    {
        get => GetValue(SwapLogoRightProperty);
        set => SetValue(SwapLogoRightProperty, value);
    }
}
