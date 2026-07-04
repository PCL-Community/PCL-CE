// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PCL.Desktop.Controls.Legacy;

public class MyScrollViewer : ScrollViewer
{
    private readonly string _scrollAnimationId = $"MyScrollViewer Scroll {Guid.NewGuid():N}";
    private double _realOffset;

    public MyScrollViewer()
    {
        PointerWheelChanged += MyScrollViewer_PointerWheelChanged;
        ScrollChanged += (_, _) => _realOffset = Offset.Y;
    }

    public static readonly StyledProperty<double> DeltaMultProperty =
        AvaloniaProperty.Register<MyScrollViewer, double>(nameof(DeltaMult), 1d);

    public double DeltaMult
    {
        get => GetValue(DeltaMultProperty);
        set => SetValue(DeltaMultProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public new void ScrollToHome()
    {
        _realOffset = 0d;
        Offset = new Vector(Offset.X, 0d);
        ModAnimation.AniStop(_scrollAnimationId);
    }

    public void PerformVerticalOffsetDelta(double delta)
    {
        double maxOffset = GetMaxVerticalOffset();
        if (maxOffset <= 0d)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaDouble(value =>
            {
                _realOffset = Math.Clamp(_realOffset + value, 0d, maxOffset);
                Offset = new Vector(Offset.X, _realOffset);
            }, delta * DeltaMult, 300, ease: new ModAnimation.AniEaseOutFluent((ModAnimation.AniEasePower)6)),
            _scrollAnimationId);
    }

    private void MyScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Math.Abs(e.Delta.Y) < 0.0001d || GetMaxVerticalOffset() <= 0d)
            return;

        if (ShouldLetChildHandleWheel(e.Source))
            return;

        e.Handled = true;
        PerformVerticalOffsetDelta(-e.Delta.Y * 120d);
    }

    private static bool ShouldLetChildHandleWheel(object? source) =>
        source is ComboBox { IsDropDownOpen: true } ||
        source is TextBox { AcceptsReturn: true } ||
        source is ComboBoxItem ||
        source is CheckBox;

    private double GetMaxVerticalOffset()
    {
        double maxOffset = Extent.Height - Viewport.Height;
        if (double.IsNaN(maxOffset) || double.IsInfinity(maxOffset))
            return 0d;

        return Math.Max(0d, maxOffset);
    }
}
