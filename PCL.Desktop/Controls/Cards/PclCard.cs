// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace PCL.Desktop.Controls.Cards;

public class PclCard : HeaderedContentControl
{
    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<PclCard, bool>(
            nameof(IsCollapsible));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PclCard, bool>(
            nameof(IsExpanded),
            defaultValue: true);

    public static readonly StyledProperty<bool> HasMouseAnimationProperty =
        AvaloniaProperty.Register<PclCard, bool>(
            nameof(HasMouseAnimation),
            defaultValue: true);

    static PclCard()
    {
        IsExpandedProperty.Changed.AddClassHandler<PclCard>(
            static (card, _) => card.UpdateCollapsedState());
    }

    public bool IsCollapsible
    {
        get => GetValue(IsCollapsibleProperty);
        set => SetValue(IsCollapsibleProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool HasMouseAnimation
    {
        get => GetValue(HasMouseAnimationProperty);
        set => SetValue(HasMouseAnimationProperty, value);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsCollapsible &&
            e.InitialPressMouseButton == MouseButton.Left &&
            e.GetPosition(this).Y <= 40)
        {
            IsExpanded = !IsExpanded;
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsCollapsible && e.Key is Key.Enter or Key.Space)
        {
            IsExpanded = !IsExpanded;
            e.Handled = true;
        }
    }

    private void UpdateCollapsedState()
    {
        if (IsExpanded)
            PseudoClasses.Remove(":collapsed");
        else
            PseudoClasses.Add(":collapsed");
    }
}
