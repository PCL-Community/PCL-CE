// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// Avalonia adapter for PCL's WPF AnimatedBackgroundGrid.
/// </summary>
public class AnimatedBackgroundGrid : Grid
{
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<AnimatedBackgroundGrid, IBrush?>(nameof(BackgroundBrush), Brushes.Transparent);

    private readonly AvaloniaProperty _animatableBrushProperty;
    private int _themeAnimationVersion;
    private bool _isBackgroundInitialized;
    private bool _isApplyingBackgroundDirect;

    // WPF exposes this lowercase field; copied control code relies on the same animation key shape.
    public int uuid { get; } = Random.Shared.Next();

    public AnimatedBackgroundGrid()
        : this(BackgroundProperty)
    {
    }

    public AnimatedBackgroundGrid(AvaloniaProperty brushProperty)
    {
        _animatableBrushProperty = brushProperty;
        AttachedToVisualTree += (_, _) => InitializeBackgroundBrush();
    }

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    protected virtual Control AnimatableElement => this;

    protected virtual IBrush? AnimatableBrush
    {
        get => Background;
        set => Background = value;
    }

    protected bool IsBackgroundAnimating { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BackgroundBrushProperty)
            return;

        if (_isApplyingBackgroundDirect)
        {
            AnimatableBrush = change.NewValue as IBrush ?? Brushes.Transparent;
            IsBackgroundAnimating = false;
            return;
        }

        ApplyBackgroundBrush(change.NewValue as IBrush);
    }

    protected void SetBackgroundBrushDirect(IBrush? brush)
    {
        brush ??= Brushes.Transparent;
        int animationVersion = ++_themeAnimationVersion;
        _isApplyingBackgroundDirect = true;
        try
        {
            BackgroundBrush = brush;
        }
        finally
        {
            _isApplyingBackgroundDirect = false;
        }

        ModAnimation.AniStop($"MyCard Theme {uuid}");
        if (animationVersion == _themeAnimationVersion)
            AnimatableBrush = brush;
        IsBackgroundAnimating = false;
        _isBackgroundInitialized = true;
    }

    private void InitializeBackgroundBrush()
    {
        if (_isBackgroundInitialized)
            return;

        AnimatableBrush = BackgroundBrush ?? Brushes.Transparent;
        IsBackgroundAnimating = false;
        _isBackgroundInitialized = true;
    }

    private void ApplyBackgroundBrush(IBrush? brush)
    {
        brush ??= Brushes.Transparent;
        if (!_isBackgroundInitialized || !ControlVisualHelpers.ShouldAnimate(this))
        {
            SetBackgroundBrushDirect(brush);
            return;
        }

        int animationVersion = ++_themeAnimationVersion;
        IsBackgroundAnimating = true;
        ModAnimation.AniStop($"MyCard Theme {uuid}");
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaColor(AnimatableElement, _animatableBrushProperty, ToSolidColor(brush), 300),
                ModAnimation.AaCode(() =>
                {
                    if (animationVersion != _themeAnimationVersion)
                        return;

                    AnimatableBrush = brush;
                    IsBackgroundAnimating = false;
                }, after: true)
            },
            $"MyCard Theme {uuid}");
    }

    private static Color ToSolidColor(IBrush brush) =>
        brush switch
        {
            ISolidColorBrush solid => solid.Color,
            _ => Colors.Transparent
        };
}
