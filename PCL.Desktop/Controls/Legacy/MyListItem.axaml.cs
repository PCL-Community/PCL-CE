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
    private readonly TextBlock? _info;
    private Border? _checkIndicator;
    private Grid? _logoHost;
    private PathShape? _logoPath;
    private SvgIcon? _svgIcon;
    private bool _isSyncingRadioGroup;
    private bool _isPressed;

    public MyListItem()
    {
        AvaloniaXamlLoader.Load(this);
        _title = this.FindControl<TextBlock>("LabTitle");
        _info = this.FindControl<TextBlock>("LabInfo");

        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisual();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        SizeChanged += (_, _) => RefreshLayoutMetrics();

        this.GetObservable(TitleProperty).Subscribe(text =>
        {
            if (_title is not null)
                _title.Text = text;
        });
        this.GetObservable(InfoProperty).Subscribe(text =>
        {
            if (_info is not null)
            {
                _info.Text = text;
                _info.IsVisible = !string.IsNullOrWhiteSpace(text);
            }
            RefreshLayoutMetrics();
        });
        this.GetObservable(FontSizeProperty).Subscribe(size =>
        {
            if (_title is not null)
                _title.FontSize = size;
        });
        this.GetObservable(SvgIconProperty).Subscribe(_ => EnsureLogo());
        this.GetObservable(LogoProperty).Subscribe(_ => EnsureLogo());
        this.GetObservable(LogoScaleProperty).Subscribe(_ => RefreshLayoutMetrics());
        this.GetObservable(MinPaddingRightProperty).Subscribe(_ => RefreshLayoutMetrics());
        this.GetObservable(TypeProperty).Subscribe(_ =>
        {
            RefreshCheckIndicator();
            RefreshLayoutMetrics();
        });
        this.GetObservable(CheckedProperty).Subscribe(_ =>
        {
            EnsureRadioGroupSelection();
            RefreshVisual();
        });
        this.GetObservable(ForegroundProperty).Subscribe(_ => RefreshVisual());

        RefreshLayoutMetrics();
        RefreshCheckIndicator();
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
        switch (Type)
        {
            case MyListItemType.RadioBox:
                Checked = true;
                break;
            case MyListItemType.CheckBox:
                Checked = !Checked;
                break;
        }
        RefreshVisual();
        Click?.Invoke(this, e);
        e.Handled = true;
    }

    private void EnsureLogo()
    {
        if (ColumnDefinitions.Count < 6)
            return;

        if (_logoPath is null && _svgIcon is null)
        {
            _logoHost = new Grid
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };
            Grid.SetColumn(_logoHost, 2);
            Grid.SetRowSpan(_logoHost, 4);
            _logoPath = new PathShape { Stretch = Stretch.Uniform };
            _svgIcon = new SvgIcon { Stretch = Stretch.Uniform, IsVisible = false };
            _logoHost.Children.Add(_logoPath);
            _logoHost.Children.Add(_svgIcon);
            Children.Add(_logoHost);
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
        RefreshLayoutMetrics();
        RefreshVisual();
    }

    private void RefreshLayoutMetrics()
    {
        if (ColumnDefinitions.Count < 6)
            return;

        bool isSmall = Height < 40d;
        bool hasLogo = !string.IsNullOrWhiteSpace(SvgIcon) || !string.IsNullOrWhiteSpace(Logo);

        ColumnDefinitions[0].Width = new GridLength(Type is MyListItemType.RadioBox or MyListItemType.CheckBox
            ? 6d
            : isSmall ? 4d : 2d);
        ColumnDefinitions[2].Width = new GridLength((hasLogo ? 34d : 0d) + (isSmall ? 0d : 4d));
        ColumnDefinitions[5].Width = new GridLength(MinPaddingRight);

        if (_logoHost is not null)
        {
            _logoHost.Margin = new Thickness(isSmall ? 6d : 8d, 8d, isSmall ? 4d : 6d, 8d);
            _logoHost.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
        }

        if (_title is not null)
            _title.Margin = new Thickness(4d, 0d, 0d, isSmall ? 0d : 2d);
        if (_info is not null)
            _info.Margin = new Thickness(4d, 1d, 0d, isSmall ? 0d : 1d);
    }

    private void RefreshCheckIndicator()
    {
        if (Type is MyListItemType.Clickable)
        {
            if (_checkIndicator is not null)
            {
                Children.Remove(_checkIndicator);
                _checkIndicator = null;
            }
            return;
        }

        if (_checkIndicator is not null)
            return;

        _checkIndicator = new Border
        {
            Width = 5d,
            Height = 0d,
            CornerRadius = new CornerRadius(2d),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(-1d, 0d, 0d, 0d),
            Background = new SolidColorBrush(Color.Parse("#1370f3")),
            IsHitTestVisible = false,
            Opacity = 0d
        };
        Grid.SetRowSpan(_checkIndicator, 4);
        Children.Add(_checkIndicator);
    }

    private void EnsureRadioGroupSelection()
    {
        if (_isSyncingRadioGroup || Type != MyListItemType.RadioBox || Parent is not Panel parent)
            return;

        _isSyncingRadioGroup = true;
        try
        {
            MyListItem? firstRadio = null;
            MyListItem? checkedRadio = null;
            foreach (Control child in parent.Children)
            {
                if (child is not MyListItem item || item.Type != MyListItemType.RadioBox)
                    continue;

                firstRadio ??= item;
                if (!item.Checked)
                    continue;

                if (checkedRadio is null || ReferenceEquals(item, this))
                {
                    if (checkedRadio is not null && !ReferenceEquals(checkedRadio, item))
                        checkedRadio.Checked = false;
                    checkedRadio = item;
                    continue;
                }

                item.Checked = false;
            }

            if (checkedRadio is null && firstRadio is not null)
                firstRadio.Checked = true;
        }
        finally
        {
            _isSyncingRadioGroup = false;
        }
    }

    private void RefreshVisual()
    {
        RefreshCheckIndicator();

        IBrush foregroundBrush = Checked
            ? new SolidColorBrush(Color.Parse("#1370f3"))
            : Foreground ?? new SolidColorBrush(Color.Parse("#343d4a"));
        var backgroundAlpha = _isPressed ? 0x18 : IsPointerOver ? 0x10 : 0x00;
        Background = new SolidColorBrush(Color.FromArgb((byte)backgroundAlpha, 19, 112, 243));
        if (_checkIndicator is not null)
        {
            _checkIndicator.Height = Checked ? 20d : 0d;
            _checkIndicator.Opacity = Checked ? 1d : 0d;
            _checkIndicator.Margin = new Thickness(-1d, 0d, 0d, 0d);
            _checkIndicator.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            _checkIndicator.RenderTransform = null;
        }
        if (_title is not null)
            _title.Foreground = foregroundBrush;
        if (_info is not null)
            _info.Foreground = new SolidColorBrush(Color.Parse("#8c8c8c"));
        if (_logoPath is not null)
        {
            _logoPath.Fill = foregroundBrush;
            _logoPath.Stroke = foregroundBrush;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = foregroundBrush;
    }
}
