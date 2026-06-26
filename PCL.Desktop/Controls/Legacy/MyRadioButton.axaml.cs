// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public partial class MyRadioButton : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioButton, bool>(nameof(Checked));

    private readonly TextBlock? _label;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;

    public MyRadioButton()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _path = this.FindControl<PathShape>("ShapeLogo");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");
        PointerReleased += OnPointerReleased;
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(LogoProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(CheckedProperty).Subscribe(_ => RefreshVisual());
        RefreshIcon();
        RefreshVisual();
    }

    public event EventHandler? Check;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Checked = true;
        Check?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void RefreshIcon()
    {
        var usesSvg = !string.IsNullOrWhiteSpace(SvgIcon);
        if (_path is not null)
        {
            _path.IsVisible = !usesSvg;
            if (!usesSvg && !string.IsNullOrWhiteSpace(Logo))
                _path.Data = Geometry.Parse(Logo);
        }
        if (_svgIcon is not null)
        {
            _svgIcon.IsVisible = usesSvg;
            _svgIcon.Icon = SvgIcon;
        }
    }

    private void RefreshVisual()
    {
        Background = new SolidColorBrush(Checked ? Color.Parse("#1370f3") : Color.Parse("#96c0f9"));
    }
}
