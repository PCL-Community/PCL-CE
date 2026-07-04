// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// 轻量折叠栏：一行可点击标题 + 三角，点击切换其下内容区的显示。
/// </summary>
public class MyCollapseBar : StackPanel
{
    private const double HeaderHeight = 30d;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCollapseBar, string>(nameof(Title), string.Empty);

    private readonly TextBlock _titleBlock;
    private readonly PathShape _triangle;
    private readonly Grid _header;
    private readonly StackPanel _contentPanel;
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private (MyCard card, bool useAnimation)? _parentCardState;
    private bool _isCollapsed;
    private bool _isLoaded;

    public MyCollapseBar()
    {
        Orientation = Avalonia.Layout.Orientation.Vertical;
        ClipToBounds = true;

        _titleBlock = new TextBlock
        {
            FontSize = 14d,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Margin = new Thickness(6d, 0d, 0d, 0d),
            IsHitTestVisible = false
        };
        _titleBlock.Foreground = FindBrush("ColorBrush1", "#343d4a");

        _triangle = new PathShape
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Height = 6d,
            Width = 10d,
            Margin = new Thickness(0d, 0d, 12d, 0d),
            IsHitTestVisible = false,
            Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
            RenderTransform = new RotateTransform(180d),
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative)
        };
        _triangle.Fill = FindBrush("ColorBrush1", "#343d4a");

        _header = new Grid
        {
            Height = HeaderHeight,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _header.Children.Add(_titleBlock);
        _header.Children.Add(_triangle);
        _header.PointerReleased += HeaderPointerReleased;

        _contentPanel = new StackPanel { Margin = new Thickness(6d, 2d, 0d, 0d) };

        Children.Add(_header);
        Children.Add(_contentPanel);

        AttachedToVisualTree += (_, _) => _isLoaded = true;
        DetachedFromVisualTree += (_, _) =>
        {
            _isLoaded = false;
            RestoreParentCardOnInterrupt();
            ModAnimation.AniStop($"MyCollapseBar {_uuid}");
            ModAnimation.AniStop($"MyCollapseBar Height {_uuid}");
        };
        this.GetObservable(TitleProperty).Subscribe(title => _titleBlock.Text = title);
    }

    public event EventHandler? Toggled;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public StackPanel ContentPanel => _contentPanel;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value)
                return;

            _isCollapsed = value;
            double target = value ? 0d : 180d;
            RotateTransform rotate = EnsureTriangleRotateTransform();
            if (_isLoaded)
            {
                ModAnimation.AniStart(
                    ModAnimation.AaRotateTransform(
                        _triangle,
                        target - rotate.Angle,
                        250,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                    $"MyCollapseBar {_uuid}",
                    refreshTime: true);
            }
            else
            {
                rotate.Angle = target;
            }

            if (_isLoaded && Bounds.Height > 0d)
            {
                if (value)
                    CollapseWithAnimation();
                else
                    ExpandWithAnimation();
            }
            else
            {
                _contentPanel.IsVisible = !value;
            }

            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        IsCollapsed = !IsCollapsed;
        e.Handled = true;
    }

    private void CollapseWithAnimation()
    {
        ModAnimation.AniStop($"MyCollapseBar Height {_uuid}");
        RestoreParentCardOnInterrupt();
        SilenceParentCard();

        double fullHeight = Bounds.Height;
        Height = fullHeight;

        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaHeight(
                this,
                HeaderHeight - fullHeight,
                200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            ModAnimation.AaCode(() =>
            {
                _contentPanel.IsVisible = false;
                Height = double.NaN;
                RestoreParentCard();
            }, after: true)
        }, $"MyCollapseBar Height {_uuid}");
    }

    private void ExpandWithAnimation()
    {
        ModAnimation.AniStop($"MyCollapseBar Height {_uuid}");
        RestoreParentCardOnInterrupt();
        SilenceParentCard();

        _contentPanel.IsVisible = true;
        Height = double.NaN;
        Measure(new Size(Bounds.Width, double.PositiveInfinity));
        double fullHeight = Math.Max(DesiredSize.Height, HeaderHeight);
        Height = HeaderHeight;

        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaHeight(
                this,
                fullHeight - HeaderHeight,
                200,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
            ModAnimation.AaCode(() =>
            {
                Height = double.NaN;
                RestoreParentCard();
            }, after: true)
        }, $"MyCollapseBar Height {_uuid}");
    }

    private void RestoreParentCardOnInterrupt()
    {
        if (_parentCardState is { } state)
        {
            state.card.UseAnimation = state.useAnimation;
            _parentCardState = null;
        }
    }

    private void SilenceParentCard()
    {
        if (_parentCardState is not null)
            return;

        Control? current = Parent as Control;
        while (current is not null)
        {
            if (current is MyCard card)
            {
                _parentCardState = (card, card.UseAnimation);
                card.UseAnimation = false;
                return;
            }

            current = current.Parent as Control;
        }
    }

    private void RestoreParentCard()
    {
        if (_parentCardState is { } state)
        {
            state.card.UseAnimation = state.useAnimation;
            _parentCardState = null;
        }
    }

    private RotateTransform EnsureTriangleRotateTransform()
    {
        if (_triangle.RenderTransform is RotateTransform rotate)
            return rotate;

        rotate = new RotateTransform();
        _triangle.RenderTransform = rotate;
        return rotate;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}
