// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Buttons;

public enum PclButtonColor
{
    Normal,
    Highlight,
    Danger
}

public class PclButton : Button
{
    public static readonly StyledProperty<PclButtonColor> ColorTypeProperty =
        AvaloniaProperty.Register<PclButton, PclButtonColor>(
            nameof(ColorType));

    private ScaleTransform? _scaleTransform;

    static PclButton()
    {
        ColorTypeProperty.Changed.AddClassHandler<PclButton>(
            static (button, _) => button.UpdateColorPseudoClasses());
    }

    public PclButtonColor ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Border? background =
            e.NameScope.Find<Border>("PART_Background");
        _scaleTransform = background?.RenderTransform as ScaleTransform;
        if (_scaleTransform is null)
            return;

        _scaleTransform.Transitions =
        [
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleXProperty,
                Duration = TimeSpan.FromMilliseconds(80),
                Easing = new CubicEaseOut()
            },
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleYProperty,
                Duration = TimeSpan.FromMilliseconds(80),
                Easing = new CubicEaseOut()
            }
        ];
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            SetScale(0.955);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        SetScale(1);
    }

    protected override void OnPointerCaptureLost(
        PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        SetScale(1);
    }

    private void SetScale(double scale)
    {
        if (_scaleTransform is null)
            return;
        _scaleTransform.ScaleX = scale;
        _scaleTransform.ScaleY = scale;
    }

    private void UpdateColorPseudoClasses()
    {
        SetPseudoClass(
            ":highlight",
            ColorType == PclButtonColor.Highlight);
        SetPseudoClass(
            ":danger",
            ColorType == PclButtonColor.Danger);
    }

    private void SetPseudoClass(string name, bool enabled)
    {
        if (enabled)
            PseudoClasses.Add(name);
        else
            PseudoClasses.Remove(name);
    }
}
