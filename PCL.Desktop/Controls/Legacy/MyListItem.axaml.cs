// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using PathShape = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public enum MyListItemType
{
    Clickable,
    RadioBox,
    CheckBox
}

public partial class MyListItem : Grid
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> InfoProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(Info), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<double> MinPaddingRightProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(MinPaddingRight), 4d);

    public static readonly StyledProperty<MyListItemType> TypeProperty =
        AvaloniaProperty.Register<MyListItem, MyListItemType>(nameof(Type));

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(Checked));

    public static readonly StyledProperty<bool> IsScaleAnimationEnabledProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(IsScaleAnimationEnabled), true);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(FontSize), 14d);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyListItem, IBrush?>(nameof(Foreground), new SolidColorBrush(Color.Parse("#343d4a")));

    private readonly TextBlock? _title;
    private PathShape? _logoPath;
    private SvgIcon? _svgIcon;
    private bool _isPressed;

    public MyListItem()
    {
        AvaloniaXamlLoader.Load(this);
        _title = this.FindControl<TextBlock>("LabTitle");

        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisual();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;

        this.GetObservable(TitleProperty).Subscribe(text =>
        {
            if (_title is not null)
                _title.Text = text;
        });
        this.GetObservable(SvgIconProperty).Subscribe(_ => EnsureLogo());
        this.GetObservable(LogoProperty).Subscribe(_ => EnsureLogo());
        this.GetObservable(CheckedProperty).Subscribe(_ => RefreshVisual());
        this.GetObservable(ForegroundProperty).Subscribe(_ => RefreshVisual());

        RefreshVisual();
    }

    public event EventHandler<PointerReleasedEventArgs>? Click;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
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

    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    public double MinPaddingRight
    {
        get => GetValue(MinPaddingRightProperty);
        set => SetValue(MinPaddingRightProperty, value);
    }

    public MyListItemType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    public bool IsScaleAnimationEnabled
    {
        get => GetValue(IsScaleAnimationEnabledProperty);
        set => SetValue(IsScaleAnimationEnabledProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        RefreshVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        if (Type is MyListItemType.RadioBox or MyListItemType.CheckBox)
            Checked = true;
        RefreshVisual();
        Click?.Invoke(this, e);
        e.Handled = true;
    }

    private void EnsureLogo()
    {
        if (ColumnDefinitions.Count < 3)
            return;

        if (_logoPath is null && _svgIcon is null)
        {
            var host = new Grid
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(6, 0, 2, 0),
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };
            host.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
            Grid.SetColumn(host, 2);
            Grid.SetRow(host, 1);
            Grid.SetRowSpan(host, 2);
            _logoPath = new PathShape { Stretch = Stretch.Uniform };
            _svgIcon = new SvgIcon { Stretch = Stretch.Uniform, IsVisible = false };
            host.Children.Add(_logoPath);
            host.Children.Add(_svgIcon);
            Children.Add(host);
        }

        var usesSvg = !string.IsNullOrWhiteSpace(SvgIcon);
        if (_logoPath is not null)
        {
            _logoPath.IsVisible = !usesSvg;
            if (!usesSvg && !string.IsNullOrWhiteSpace(Logo))
            {
                try
                {
                    _logoPath.Data = Geometry.Parse(Logo);
                }
                catch (FormatException)
                {
                    _logoPath.Data = null;
                }
            }
        }
        if (_svgIcon is not null)
        {
            _svgIcon.IsVisible = usesSvg;
            _svgIcon.Icon = SvgIcon;
        }
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        var accent = Checked ? Color.Parse("#1370f3") : Color.Parse("#343d4a");
        Foreground = new SolidColorBrush(accent);
        var backgroundAlpha = Checked ? 0x20 : _isPressed ? 0x18 : IsPointerOver ? 0x10 : 0x00;
        Background = new SolidColorBrush(Color.FromArgb((byte)backgroundAlpha, 19, 112, 243));
        if (_logoPath is not null)
        {
            _logoPath.Fill = Foreground;
            _logoPath.Stroke = Foreground;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = Foreground;
    }
}
