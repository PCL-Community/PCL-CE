// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

// Kept for older migrated code. New WPF-compatible code should use MyListItem.CheckType.
public enum MyListItemType
{
    None,
    Clickable,
    RadioBox,
    CheckBox
}

public partial class MyListItem : Grid, IMyRadio
{
#pragma warning disable CA1711
    public delegate void ClickEventHandler(object sender, PointerReleasedEventArgs e);

    public delegate void LogoClickEventHandler(object sender, PointerReleasedEventArgs e);

    public delegate void CheckEventHandler(object sender, RouteEventArgs e);
#pragma warning restore CA1711

    public enum CheckType
    {
        None,
        Clickable,
        RadioBox,
        CheckBox
    }

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

    public static readonly StyledProperty<CheckType> TypeProperty =
        AvaloniaProperty.Register<MyListItem, CheckType>(nameof(Type), CheckType.None);

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(Checked));

    public static readonly StyledProperty<bool> IsScaleAnimationEnabledProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(IsScaleAnimationEnabled), true);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(FontSize), 14d);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyListItem, IBrush?>(nameof(Foreground));

    private readonly TextBlock? _title;
    private readonly TextBlock? _info;
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private Border? _rectBack;
    private Border? _checkIndicator;
    private Grid? _logoHost;
    private PathShape? _logoPath;
    private SvgIcon? _svgIcon;
    private Image? _logoImage;
    private Control? _buttonStack;
    private StackPanel? _panTags;
    private readonly InlineCollection _pendingInlines = new();
    private bool _isLogoPressed;
    private bool _isSyncingRadioGroup;
    private bool _isSettingChecked;
    private bool _isPressed;
    private bool _isLoaded;
    private string? _lastColorState;

#pragma warning disable IDE1006, CA1051
    public bool isMouseOverAnimationEnabled = true;

    public object? tag
    {
        get => Tag;
        set => Tag = value;
    }

    public int Uuid { get; } = Random.Shared.Next();

    public Control? buttonStack => _buttonStack;

    public Control? pathLogo => _logoHost;

    public Border? rectCheck => _checkIndicator;
#pragma warning restore IDE1006, CA1051

    public MyListItem()
    {
        AvaloniaXamlLoader.Load(this);
        _title = this.FindControl<TextBlock>("LabTitle");
        _info = this.FindControl<TextBlock>("LabInfo");
        _pendingInlines.Clear();

        PointerEntered += (_, _) => RefreshColor(animate: true);
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshColor(animate: true);
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        SizeChanged += (_, _) => RefreshLayoutMetrics();
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            SyncTextVisuals();
            RefreshCheckedVisual(animate: false);
            RefreshColor(animate: false);
        };

        this.GetObservable(TitleProperty).Subscribe(text =>
        {
            SetTitleText(text);
        });
        this.GetObservable(InfoProperty).Subscribe(text =>
        {
            if (_info is not null)
            {
                SetInfoText(text);
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
        this.GetObservable(MinPaddingRightProperty).Subscribe(_ =>
        {
            RefreshLayoutMetrics();
            if (_buttonStack is not null && !IsPointerOver)
                SetRightPaddingWidth(MinPaddingRight);
        });
        this.GetObservable(TypeProperty).Subscribe(_ =>
        {
            RefreshCheckIndicator();
            RefreshLayoutMetrics();
            RefreshCheckedVisual(_isLoaded);
        });
        this.GetObservable(CheckedProperty).Subscribe(_ =>
        {
            EnsureRadioGroupSelection();
            RefreshCheckedVisual(_isLoaded);
            RefreshColor(_isLoaded);
        });
        this.GetObservable(ForegroundProperty).Subscribe(_ => ApplyForegroundBrush());

        SyncTextVisuals();
        RefreshLayoutMetrics();
        RefreshCheckIndicator();
        RefreshCheckedVisual(animate: false);
        RefreshColor(animate: false);
    }

    public event EventHandler<PointerReleasedEventArgs>? Click;

    public event EventHandler<PointerReleasedEventArgs>? LogoClick;

    public event IMyRadio.CheckEventHandler? Check;

    public event IMyRadio.ChangedEventHandler? Changed;

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

    public CheckType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, user: false);
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

    public IList<MyIconButton> Buttons
    {
        get => field;
        set
        {
            field = value;
            ApplyButtons();
            RefreshLayoutMetrics();
        }
    } = [];

    public InlineCollection Inlines =>
        _title?.Inlines ?? _pendingInlines;

    public bool LogoClickable
    {
        get => field;
        set
        {
            field = value;
            if (_logoHost is not null)
                _logoHost.IsHitTestVisible = value;
        }
    }

    public Action<MyListItem, EventArgs>? ContentHandler { get; set; }

    /// <summary>
    /// WPF 版 MyListItem 的标签公开面。实例列表、下载列表会在延迟构建菜单时写入这里。
    /// </summary>
    public object? Tags
    {
        get => field;
        set
        {
            field = value;
            ApplyTags(value);
        }
    }

    public int PaddingLeft
    {
        get => ColumnDefinitions.Count > 1 ? (int)Math.Round(ColumnDefinitions[1].Width.Value) : 0;
        set
        {
            if (ColumnDefinitions.Count > 1)
                ColumnDefinitions[1].Width = new GridLength(Math.Max(0, value));
        }
    }

    public void SetChecked(bool value, bool user = false, bool animate = true)
    {
        if (Checked == value && !_isSettingChecked)
            return;

        bool oldValue = Checked;
        RouteEventArgs changedArgs = new(user);
        _isSettingChecked = true;
        try
        {
            SetValue(CheckedProperty, value);
            Changed?.Invoke(this, changedArgs);
            if (changedArgs.Handled)
            {
                SetValue(CheckedProperty, oldValue);
                return;
            }
        }
        finally
        {
            _isSettingChecked = false;
        }

        if (value && user)
            Check?.Invoke(this, new RouteEventArgs(user));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        RefreshColor(animate: true);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        Click?.Invoke(this, e);
        if (e.Handled)
        {
            RefreshColor(animate: true);
            return;
        }

        switch (Type)
        {
            case CheckType.RadioBox:
                SetChecked(true, user: true);
                break;
            case CheckType.CheckBox:
                SetChecked(!Checked, user: true);
                break;
        }
        RefreshColor(animate: true);
        e.Handled = true;
    }

    private Border EnsureRectBack()
    {
        if (_rectBack is not null)
            return _rectBack;

        _rectBack = new Border
        {
            Name = "RectBack",
            CornerRadius = new CornerRadius(IsScaleAnimationEnabled || Height > 40d ? 6d : 0d),
            RenderTransform = IsScaleAnimationEnabled ? new ScaleTransform(0.8d, 0.8d) : null,
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
            BorderThickness = new Thickness(1d),
            IsHitTestVisible = false,
            Opacity = 0d,
            Background = FindBrush("ColorBrush7", "#e0eafd"),
            BorderBrush = FindBrush("ColorBrush6", "#d5e6fd")
        };
        Grid.SetColumnSpan(_rectBack, 999);
        Grid.SetRowSpan(_rectBack, 999);
        Children.Insert(0, _rectBack);
        return _rectBack;
    }

    private void EnsureLogo()
    {
        if (ColumnDefinitions.Count < 6)
            return;

        if (_logoPath is null && _svgIcon is null && _logoImage is null)
        {
            _logoHost = new Grid
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                IsHitTestVisible = LogoClickable
            };
            _logoHost.PointerPressed += LogoPointerPressed;
            _logoHost.PointerReleased += LogoPointerReleased;
            _logoHost.PointerExited += (_, _) => _isLogoPressed = false;
            Grid.SetColumn(_logoHost, 2);
            Grid.SetRowSpan(_logoHost, 4);
            _logoPath = new PathShape { Stretch = Stretch.Uniform };
            _svgIcon = new SvgIcon { Stretch = Stretch.Uniform, IsVisible = false };
            _logoImage = new Image
            {
                Stretch = Stretch.Uniform,
                IsVisible = false,
                IsHitTestVisible = false
            };
            _logoHost.Children.Add(_logoPath);
            _logoHost.Children.Add(_svgIcon);
            _logoHost.Children.Add(_logoImage);
            Children.Add(_logoHost);
        }

        var usesSvg = !string.IsNullOrWhiteSpace(SvgIcon);
        var usesImage = !usesSvg && IsImageLogo(Logo);
        if (_logoPath is not null)
        {
            _logoPath.IsVisible = !usesSvg && !usesImage;
            if (!usesSvg && !usesImage && !string.IsNullOrWhiteSpace(Logo))
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
        if (_logoImage is not null)
        {
            _logoImage.IsVisible = usesImage;
            _logoImage.Source = usesImage ? LoadLogoImage(Logo) : null;
        }
        RefreshLayoutMetrics();
        ApplyForegroundBrush();
        RefreshColor(_isLoaded);
    }

    private void RefreshLayoutMetrics()
    {
        if (ColumnDefinitions.Count < 6)
            return;

        bool isSmall = Height < 40d;
        bool hasLogo = !string.IsNullOrWhiteSpace(SvgIcon) || !string.IsNullOrWhiteSpace(Logo);

        ColumnDefinitions[0].Width = new GridLength(Type is CheckType.RadioBox or CheckType.CheckBox
            ? 6d
            : isSmall ? 4d : 2d);
        ColumnDefinitions[2].Width = new GridLength((hasLogo ? 34d : 0d) + (isSmall ? 0d : 4d));

        if (_logoHost is not null)
        {
            _logoHost.Margin = new Thickness(isSmall ? 6d : 8d, 8d, isSmall ? 4d : 6d, 8d);
            _logoHost.RenderTransform = new ScaleTransform(LogoScale, LogoScale);
        }
        if (_rectBack is not null)
            _rectBack.CornerRadius = new CornerRadius(IsScaleAnimationEnabled || Height > 40d ? 6d : 0d);

        if (_title is not null)
            _title.Margin = new Thickness(4d, 0d, 0d, isSmall ? 0d : 2d);
        if (_info is not null)
            _info.Margin = new Thickness(4d, 1d, 0d, isSmall ? 0d : 1d);
    }

    private void RefreshCheckIndicator()
    {
        if (Type is CheckType.None or CheckType.Clickable)
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
            Background = FindBrush("ColorBrush3", "#1370f3"),
            IsHitTestVisible = false,
            Opacity = 0d
        };
        Grid.SetRowSpan(_checkIndicator, 4);
        Children.Add(_checkIndicator);
    }

    private void EnsureRadioGroupSelection()
    {
        if (_isSyncingRadioGroup || Type != CheckType.RadioBox || !Checked || Parent is not Panel parent)
            return;

        _isSyncingRadioGroup = true;
        try
        {
            MyListItem? checkedRadio = null;
            foreach (Control child in parent.Children)
            {
                if (child is not MyListItem item || item.Type != CheckType.RadioBox)
                    continue;

                if (!item.Checked)
                    continue;

            if (checkedRadio is null || ReferenceEquals(item, this))
            {
                if (checkedRadio is not null && !ReferenceEquals(checkedRadio, item))
                    checkedRadio.SetChecked(false, user: false);
                checkedRadio = item;
                continue;
            }

                item.SetChecked(false, user: false);
            }
        }
        finally
        {
            _isSyncingRadioGroup = false;
        }
    }

    private void RefreshCheckedVisual(bool animate)
    {
        RefreshCheckIndicator();

        const double checkedHeight = 20d;
        string foregroundName = Checked
            ? Height < 40d ? "ColorBrush3" : "ColorBrush2"
            : "ColorBrush1";

        if (_checkIndicator is null)
        {
            Foreground = FindBrush(foregroundName, Checked ? "#1370f3" : "#343d4a");
            return;
        }

        ModAnimation.AniStop($"MyListItem Checked {_uuid}");
        _checkIndicator.Margin = new Thickness(-1d, 0d, 0d, 0d);
        _checkIndicator.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        if (!animate)
        {
            _checkIndicator.Height = Checked ? checkedHeight : 0d;
            _checkIndicator.Opacity = Checked ? 1d : 0d;
            _checkIndicator.RenderTransform = null;
            Foreground = FindBrush(foregroundName, Checked ? "#1370f3" : "#343d4a");
            return;
        }

        List<ModAnimation.AniData> animations = [];
        if (Checked)
        {
            _checkIndicator.Height = checkedHeight;
            _checkIndicator.Opacity = 1d;
            ScaleTransform scale = new(1d, 0d);
            _checkIndicator.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
            _checkIndicator.RenderTransform = scale;
            animations.Add(ModAnimation.AaDouble(
                value => scale.ScaleY = Math.Max(0d, scale.ScaleY + value),
                1d - scale.ScaleY,
                300,
                ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaColor(this, ForegroundProperty, foregroundName, 200));
        }
        else
        {
            _checkIndicator.Height = checkedHeight;
            if (_checkIndicator.RenderTransform is not ScaleTransform scale)
            {
                scale = new ScaleTransform(1d, 1d);
                _checkIndicator.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
                _checkIndicator.RenderTransform = scale;
            }
            animations.Add(ModAnimation.AaDouble(
                value => scale.ScaleY = Math.Max(0d, scale.ScaleY + value),
                -scale.ScaleY,
                120,
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaOpacity(_checkIndicator, -_checkIndicator.Opacity, 70, 40));
            animations.Add(ModAnimation.AaColor(this, ForegroundProperty, foregroundName, 120));
            animations.Add(ModAnimation.AaCode(() =>
            {
                _checkIndicator.Height = 0d;
                _checkIndicator.RenderTransform = null;
            }, after: true));
        }

        ModAnimation.AniStart(animations, $"MyListItem Checked {_uuid}");
    }

    private void RefreshColor(bool animate)
    {
        string stateNew;
        int time;
        if (_isPressed && !(Type == CheckType.RadioBox && Checked))
        {
            stateNew = "MouseDown";
            time = 120;
        }
        else if (IsPointerOver && isMouseOverAnimationEnabled)
        {
            stateNew = "MouseOver";
            time = 120;
        }
        else
        {
            stateNew = "Idle";
            time = 180;
        }

        if (_lastColorState == stateNew)
            return;
        _lastColorState = stateNew;
        RunDeferredContentHandler();

        if (!animate)
        {
            ModAnimation.AniStop($"ListItem Color {_uuid}");
            if (stateNew is "MouseDown" or "MouseOver")
            {
                Border rect = EnsureRectBack();
                SetRightPaddingWidth(GetExpandedPaddingRight());
                rect.Background = FindBrush(stateNew == "MouseDown" ? "ColorBrush6" : "ColorBrushBg1", "#bee0eafd");
                rect.Opacity = 1d;
                rect.RenderTransform = IsScaleAnimationEnabled ? new ScaleTransform(1d, 1d) : null;
                RenderTransform = new ScaleTransform(1d, 1d);
                SetButtonStackOpacity(1d);
            }
            else
            {
                RenderTransform = new ScaleTransform(1d, 1d);
                SetButtonStackOpacity(0d);
                SetRightPaddingWidth(MinPaddingRight);
                if (_rectBack is not null)
                {
                    _rectBack.Background = FindBrush("ColorBrush7", "#e0eafd");
                    _rectBack.Opacity = 0d;
                    if (IsScaleAnimationEnabled)
                        _rectBack.RenderTransform = new ScaleTransform(0.75d, 0.75d);
                }
            }
            return;
        }

        List<ModAnimation.AniData> animations = [];
        if (stateNew is "MouseDown" or "MouseOver")
        {
            Border rect = EnsureRectBack();
            if (_buttonStack is not null)
            {
                animations.Add(ModAnimation.AaOpacity(
                    _buttonStack,
                    1d - _buttonStack.Opacity,
                    (int)Math.Round(time * 0.7d),
                    (int)Math.Round(time * 0.3d)));
                animations.Add(ModAnimation.AaDouble(
                    value => SetRightPaddingWidth(GetRightPaddingWidth() + value),
                    GetExpandedPaddingRight() - GetRightPaddingWidth(),
                    (int)Math.Round(time * 0.3d),
                    (int)Math.Round(time * 0.7d)));
            }
            animations.Add(ModAnimation.AaColor(
                rect,
                Border.BackgroundProperty,
                stateNew == "MouseDown" ? "ColorBrush6" : "ColorBrushBg1",
                time));
            animations.Add(ModAnimation.AaOpacity(rect, 1d - rect.Opacity, time, ease: new ModAnimation.AniEaseOutFluent()));
            if (IsScaleAnimationEnabled)
            {
                animations.Add(ModAnimation.AaScaleTransform(
                    rect,
                    1d - GetScaleX(rect),
                    (int)Math.Round(time * 1.6d),
                    ease: new ModAnimation.AniEaseOutFluent()));
                animations.Add(ModAnimation.AaScaleTransform(
                    this,
                    (stateNew == "MouseDown" ? 0.98d : 1d) - GetScaleX(this),
                    stateNew == "MouseDown" ? (int)Math.Round(time * 0.9d) : (int)Math.Round(time * 1.2d),
                    ease: new ModAnimation.AniEaseOutFluent()));
            }
        }
        else
        {
            if (_buttonStack is not null)
            {
                animations.Add(ModAnimation.AaOpacity(_buttonStack, -_buttonStack.Opacity, (int)Math.Round(time * 0.4d)));
                animations.Add(ModAnimation.AaDouble(
                    value => SetRightPaddingWidth(GetRightPaddingWidth() + value),
                    MinPaddingRight - GetRightPaddingWidth(),
                    (int)Math.Round(time * 0.4d)));
            }
            if (_rectBack is not null)
            {
                animations.Add(ModAnimation.AaOpacity(_rectBack, -_rectBack.Opacity, time));
                if (IsScaleAnimationEnabled)
                {
                    animations.Add(ModAnimation.AaColor(_rectBack, Border.BackgroundProperty, "ColorBrush7", time));
                    animations.Add(ModAnimation.AaScaleTransform(
                        this,
                        1d - GetScaleX(this),
                        time * 3,
                        ease: new ModAnimation.AniEaseOutFluent()));
                    animations.Add(ModAnimation.AaScaleTransform(
                        _rectBack,
                        0.996d - GetScaleX(_rectBack),
                        time,
                        ease: new ModAnimation.AniEaseOutFluent()));
                    animations.Add(ModAnimation.AaScaleTransform(_rectBack, -0.246d, 1, after: true));
                }
            }
        }

        ModAnimation.AniStart(animations, $"ListItem Color {_uuid}");
    }

    private void ApplyButtons()
    {
        if (_buttonStack is not null)
        {
            Children.Remove(_buttonStack);
            _buttonStack = null;
        }

        if (Buttons.Count == 0)
            return;

        if (Buttons.Count == 1)
        {
            MyIconButton button = Buttons[0];
            PrepareButton(button);
            _buttonStack = button;
            Children.Add(button);
            return;
        }

        StackPanel stack = new()
        {
            Opacity = 0d,
            Margin = new Thickness(0d, 0d, 5d, 0d),
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsHitTestVisible = true
        };
        foreach (MyIconButton button in Buttons)
        {
            if (double.IsNaN(button.Height))
                button.Height = 25d;
            if (double.IsNaN(button.Width))
                button.Width = 25d;
            stack.Children.Add(button);
        }

        Grid.SetColumnSpan(stack, 10);
        Grid.SetRowSpan(stack, 10);
        _buttonStack = stack;
        Children.Add(stack);
    }

    private static void PrepareButton(MyIconButton button)
    {
        if (double.IsNaN(button.Height))
            button.Height = 25d;
        if (double.IsNaN(button.Width))
            button.Width = 25d;
        button.Opacity = 0d;
        button.Margin = new Thickness(0d, 0d, 5d, 0d);
        button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Grid.SetColumnSpan(button, 10);
        Grid.SetRowSpan(button, 10);
    }

    private void SetButtonStackOpacity(double opacity)
    {
        if (_buttonStack is not null)
            _buttonStack.Opacity = opacity;
    }

    private double GetRightPaddingWidth() =>
        ColumnDefinitions.Count > 5 ? ColumnDefinitions[5].Width.Value : MinPaddingRight;

    private void SetRightPaddingWidth(double value)
    {
        if (ColumnDefinitions.Count > 5)
            ColumnDefinitions[5].Width = new GridLength(Math.Max(0d, value));
    }

    private double GetExpandedPaddingRight() =>
        Math.Max(MinPaddingRight, 5d + Buttons.Count * 25d);

    private void ApplyForegroundBrush()
    {
        IBrush foregroundBrush = Foreground ?? FindBrush("ColorBrush1", "#343d4a");
        if (_title is not null)
            _title.Foreground = foregroundBrush;
        if (_info is not null)
            _info.Foreground = FindBrush("ColorBrushGray2", "#737373");
        if (_logoPath is not null)
        {
            _logoPath.Fill = foregroundBrush;
            _logoPath.Stroke = foregroundBrush;
        }
        if (_svgIcon is not null)
            _svgIcon.IconBrush = foregroundBrush;
    }

    private void SyncTextVisuals()
    {
        if (_title is not null)
            SetTitleText(Title);
        if (_info is not null)
        {
            SetInfoText(Info);
            _info.IsVisible = !string.IsNullOrWhiteSpace(Info);
        }
        ApplyForegroundBrush();
    }

    private void SetTitleText(string text)
    {
        if (_title is null)
            return;

        _title.Inlines = null;
        _title.Text = text;
        _title.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void SetInfoText(string text)
    {
        if (_info is null)
            return;

        _info.Inlines = null;
        _info.Text = text;
        _info.InvalidateMeasure();
        InvalidateMeasure();
    }

    private StackPanel EnsurePanTags()
    {
        if (_panTags is not null)
            return _panTags;

        _panTags = new StackPanel
        {
            IsHitTestVisible = false,
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(3.5d, 0d, -3d, 0d),
            IsVisible = false
        };
        Grid.SetColumn(_panTags, 3);
        Grid.SetRow(_panTags, 2);
        Children.Add(_panTags);
        return _panTags;
    }

    private void ApplyTags(object? value)
    {
        StackPanel panel = EnsurePanTags();
        panel.Children.Clear();

        List<string> tags = value switch
        {
            null => [],
            string text => text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            IEnumerable<string> list => list.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
            System.Collections.IEnumerable list => list.Cast<object?>()
                .Select(static item => item?.ToString())
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item!)
                .ToList(),
            _ => [value.ToString() ?? string.Empty]
        };
        tags.RemoveAll(static item => string.IsNullOrWhiteSpace(item));

        panel.IsVisible = tags.Count > 0;
        if (_info is not null)
        {
            Grid.SetColumn(_info, tags.Count > 0 ? 4 : 3);
            Grid.SetColumnSpan(_info, tags.Count > 0 ? 1 : 2);
            _info.Margin = new Thickness(tags.Count > 0 ? 4d : 4d, 1d, 0d, 0d);
        }

        foreach (string tagText in tags)
        {
            Border tag = new()
            {
                Background = new SolidColorBrush(Color.FromArgb(17, 0, 0, 0)),
                Padding = new Thickness(3d, 1d, 3d, 1d),
                CornerRadius = new CornerRadius(3d),
                Margin = new Thickness(0d, 0d, 3d, 0d),
                Child = new TextBlock
                {
                    Text = tagText,
                    Foreground = new SolidColorBrush(Color.FromRgb(134, 134, 134)),
                    FontSize = 11d
                }
            };
            panel.Children.Add(tag);
        }
    }

    private static bool IsImageLogo(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo))
            return false;

        string normalized = NormalizeLogoUri(logo);
        return normalized.StartsWith("avares://", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
    }

    private static Bitmap? LoadLogoImage(string logo)
    {
        try
        {
            string normalized = NormalizeLogoUri(logo);
            if (File.Exists(normalized))
            {
                using Stream fileStream = File.OpenRead(normalized);
                return new Bitmap(fileStream);
            }

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
                return null;

            using Stream stream = uri.IsFile ? File.OpenRead(uri.LocalPath) : AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string NormalizeLogoUri(string logo)
    {
        const string wpfImagePrefix = "pack://application:,,,/images/";
        if (logo.StartsWith(wpfImagePrefix, StringComparison.OrdinalIgnoreCase))
            return "avares://PCL.Desktop/WpfOriginal/Images/" + logo[wpfImagePrefix.Length..];

        return logo;
    }

    private static double GetScaleX(Control control) =>
        control.RenderTransform switch
        {
            ScaleTransform scale => scale.ScaleX,
            TransformGroup group => group.Children.OfType<ScaleTransform>().FirstOrDefault()?.ScaleX ?? 1d,
            _ => 1d
        };

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }

    private void LogoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!LogoClickable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isLogoPressed = true;
        e.Handled = true;
    }

    private void LogoPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!LogoClickable || !_isLogoPressed)
            return;

        _isLogoPressed = false;
        LogoClick?.Invoke(this, e);
        e.Handled = true;
    }

    private void RunDeferredContentHandler()
    {
        Action<MyListItem, EventArgs>? handler = ContentHandler;
        if (handler is null)
            return;

        ContentHandler = null;
        handler(this, EventArgs.Empty);
    }
}
