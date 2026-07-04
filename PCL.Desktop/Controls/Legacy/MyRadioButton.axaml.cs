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
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public partial class MyRadioButton : Border
{
#pragma warning disable CA1711
    public delegate void ChangeEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void PreviewClickEventHandler(object sender, RouteEventArgs e);
#pragma warning restore CA1711

    public enum ColorState
    {
        White,
        Highlight
    }

    private const int AnimationTimeOfMouseIn = 90;
    private const int AnimationTimeOfMouseOut = 150;
    private const int AnimationTimeOfCheck = 120;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioButton, bool>(nameof(Checked));

    private readonly TextBlock? _label;
    private readonly Grid? _logoHost;
    private readonly PathShape? _path;
    private readonly SvgIcon? _svgIcon;
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private bool _hasLegacyLogo;
    private bool _isLoaded;
    private bool _isMouseDown;
    private bool _isUpdatingChecked;

    public MyRadioButton()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _logoHost = this.FindControl<Grid>("LogoHost");
        _path = this.FindControl<PathShape>("ShapeLogo");
        _svgIcon = this.FindControl<SvgIcon>("ShapeSvgIcon");

        Focusable = true;
        PointerPressed += RadioboxPointerPressed;
        PointerReleased += RadioboxPointerReleased;
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) =>
        {
            RadioboxMouseLeave();
            RefreshColor();
        };
        KeyDown += RadioboxKeyDown;
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            RefreshLogoHostVisibility();
            RefreshColor(forceDisableAnimation: true);
            EnsureSingleCheckedInParent(false);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isLoaded = false;
            StopAnimations();
        };

        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(LogoProperty).Subscribe(value => ApplyLegacyLogo(value));
        this.GetObservable(SvgIconProperty).Subscribe(_ => ApplySvgIcon());
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());

        RefreshLogoHostVisibility();
        RefreshColor(forceDisableAnimation: true);
    }

    public event CheckEventHandler? Check;

    public event ChangeEventHandler? Change;

    public event PreviewClickEventHandler? PreviewClick;

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

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false, true);
    }

    public InlineCollection Inlines => _label?.Inlines ?? throw new InvalidOperationException("MyRadioButton text block is not initialized.");

    public ColorState ColorType
    {
        get;
        set
        {
            field = value;
            RefreshColor();
        }
    } = ColorState.White;

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1d : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    public void SetChecked(bool value, bool raiseByMouse, bool anime = true)
    {
        bool isChanged = Checked != value;
        if (isChanged)
            SetCheckedValue(value);

        EnsureSingleCheckedInParent(anime);

        if (!isChanged)
            return;

        RefreshColor(forceDisableAnimation: !anime);
        if (Checked)
            Check?.Invoke(this, raiseByMouse);
        Change?.Invoke(this, raiseByMouse);
    }

    public void RefreshMyRadioButtonColor() => RefreshColor();

    private void RadioboxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || Checked || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
        Focus();
        RefreshColor();
        e.Handled = true;
    }

    private void RadioboxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEnabled || Checked || e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;

        _isMouseDown = false;
        RouteEventArgs previewArgs = new(raiseByMouse: true);
        PreviewClick?.Invoke(this, previewArgs);
        if (!previewArgs.Handled)
            SetChecked(true, true, true);

        e.Handled = true;
    }

    private void RadioboxKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEnabled || Checked || (e.Key != Key.Enter && e.Key != Key.Space))
            return;

        RouteEventArgs previewArgs = new(raiseByMouse: false);
        PreviewClick?.Invoke(this, previewArgs);
        if (!previewArgs.Handled)
            SetChecked(true, false, true);
        e.Handled = true;
    }

    private void RadioboxMouseLeave() => _isMouseDown = false;

    private void EnsureSingleCheckedInParent(bool anime)
    {
        if (_isUpdatingChecked || Parent is not Panel panel)
            return;

        List<MyRadioButton> siblings = [];
        foreach (Control child in panel.Children)
        {
            if (child is MyRadioButton radioButton)
                siblings.Add(radioButton);
        }

        if (siblings.Count == 0)
            return;

        int checkedCount = siblings.Count(static radioButton => radioButton.Checked);
        if (checkedCount == 0)
        {
            siblings[0].SetChecked(true, false, anime);
            return;
        }

        if (checkedCount <= 1)
            return;

        _isUpdatingChecked = true;
        try
        {
            if (Checked)
            {
                foreach (MyRadioButton radioButton in siblings)
                {
                    if (!ReferenceEquals(radioButton, this) && radioButton.Checked)
                        radioButton.SetCheckedFromGroup(false, anime);
                }
            }
            else
            {
                bool foundChecked = false;
                foreach (MyRadioButton radioButton in siblings)
                {
                    if (!radioButton.Checked)
                        continue;

                    if (!foundChecked)
                    {
                        foundChecked = true;
                        continue;
                    }

                    radioButton.SetCheckedFromGroup(false, anime);
                }
            }
        }
        finally
        {
            _isUpdatingChecked = false;
        }
    }

    private void SetCheckedFromGroup(bool value, bool anime)
    {
        if (Checked == value)
            return;

        SetCheckedValue(value);
        RefreshColor(forceDisableAnimation: !anime);
        Change?.Invoke(this, false);
    }

    private void SetCheckedValue(bool value)
    {
        _isUpdatingChecked = true;
        try
        {
            SetCurrentValue(CheckedProperty, value);
        }
        finally
        {
            _isUpdatingChecked = false;
        }
    }

    private void ApplyLegacyLogo(string? value)
    {
        if (_path is null)
            return;

        _hasLegacyLogo = !string.IsNullOrWhiteSpace(value);
        if (!_hasLegacyLogo)
        {
            _path.Data = null;
        }
        else
        {
            try
            {
                _path.Data = Geometry.Parse(value!);
            }
            catch (FormatException)
            {
                _path.Data = null;
                _hasLegacyLogo = false;
            }
        }

        ApplyIconVisibility();
        RefreshLogoHostVisibility();
    }

    private void ApplySvgIcon()
    {
        if (_svgIcon is not null)
            _svgIcon.Icon = SvgIcon;

        ApplyIconVisibility();
        ApplyLogoScale();
        RefreshLogoHostVisibility();
        RefreshColor();
    }

    private void ApplyIconVisibility()
    {
        bool usesSvg = IsUsingSvgIcon;
        if (_path is not null)
            _path.IsVisible = !usesSvg;
        if (_svgIcon is not null)
            _svgIcon.IsVisible = usesSvg;
    }

    private void ApplyLogoScale()
    {
        if (_logoHost is not null)
            _logoHost.RenderTransform = new ScaleTransform(EffectiveLogoScale, EffectiveLogoScale);
    }

    private void RefreshLogoHostVisibility()
    {
        if (_logoHost is null || _label is null)
            return;

        _logoHost.IsVisible = true;
        _logoHost.Width = HasAnyIcon ? 16d : 0d;
        _logoHost.Height = 16d;
        _logoHost.Margin = new Thickness(12d, 0d, 0d, 0d);
        _label.Margin = new Thickness(8d, 0d, 12d, 0d);
        ApplyLogoScale();
    }

    private void RefreshColor(bool forceDisableAnimation = false)
    {
        if (_label is null || _path is null)
            return;

        bool animate = _isLoaded && ModAnimation.AniControlEnabled == 0 && !forceDisableAnimation;
        if (!animate)
        {
            ApplyNonAnimatedColor();
            return;
        }

        ModAnimation.AniStart(GetTextAndIconColorAnimations(), $"MyRadioButton Checked {_uuid}");
        ModAnimation.AniStart(
            CreateColorAnimation(this, BackgroundProperty, GetBackgroundResourceOrColor(), GetBackgroundAnimationTime()),
            $"MyRadioButton Color {_uuid}");
    }

    private List<ModAnimation.AniData> GetTextAndIconColorAnimations()
    {
        int duration = GetForegroundAnimationTime();
        object foreground = GetForegroundResourceOrColor();
        List<ModAnimation.AniData> animations = [];
        if (_path is not null)
            animations.Add(CreateColorAnimation(_path, Shape.FillProperty, foreground, duration));
        if (_svgIcon is not null && _svgIcon.IsVisible)
            animations.Add(CreateColorAnimation(_svgIcon, PCL.Desktop.Controls.Legacy.SvgIcon.IconBrushProperty, foreground, duration));
        if (_label is not null)
            animations.Add(CreateColorAnimation(_label, TextBlock.ForegroundProperty, foreground, duration));
        return animations;
    }

    private object GetForegroundResourceOrColor()
    {
        if (!IsEnabled)
            return "ColorBrushGray5";

        return ColorType switch
        {
            ColorState.White => Checked ? "ColorBrush3" : Colors.White,
            ColorState.Highlight => Checked ? Colors.White : IsPointerOver ? "ColorBrush3" : "ColorBrush3",
            _ => Colors.White
        };
    }

    private object GetBackgroundResourceOrColor()
    {
        if (!IsEnabled)
            return "ColorBrushSemiTransparent";

        if (ColorType == ColorState.Highlight)
        {
            if (Checked)
                return "ColorBrush3";
            if (_isMouseDown)
                return "ColorBrush6";
            if (IsPointerOver)
                return "ColorBrush7";
            return "ColorBrushSemiTransparent";
        }

        if (Checked)
            return Colors.White;
        if (_isMouseDown)
            return Color.FromArgb(120, 234, 242, 254);
        if (IsPointerOver)
            return Color.FromArgb(50, 234, 242, 254);
        return "ColorBrushSemiTransparent";
    }

    private int GetForegroundAnimationTime()
    {
        if (Checked)
            return AnimationTimeOfCheck;
        return IsPointerOver ? AnimationTimeOfMouseIn : AnimationTimeOfMouseOut;
    }

    private int GetBackgroundAnimationTime()
    {
        if (Checked)
            return AnimationTimeOfCheck;
        if (_isMouseDown)
            return 60;
        return IsPointerOver ? AnimationTimeOfMouseIn : AnimationTimeOfMouseOut;
    }

    private void ApplyNonAnimatedColor()
    {
        StopAnimations();
        Background = ResolveBrush(GetBackgroundResourceOrColor(), "#01eaf2fe");
        IBrush foreground = ResolveBrush(GetForegroundResourceOrColor(), "#ffffff");
        if (_path is not null)
            _path.Fill = foreground;
        if (_svgIcon is not null)
            _svgIcon.IconBrush = foreground;
        if (_label is not null)
            _label.Foreground = foreground;
    }

    private void StopAnimations()
    {
        ModAnimation.AniStop($"MyRadioButton Checked {_uuid}");
        ModAnimation.AniStop($"MyRadioButton Color {_uuid}");
    }

    private static ModAnimation.AniData CreateColorAnimation(Control control, AvaloniaProperty property, object color, int duration) =>
        color switch
        {
            string resource => ModAnimation.AaColor(control, property, resource, duration),
            Color target => ModAnimation.AaColor(control, property, target, duration),
            _ => ModAnimation.AaColor(control, property, Colors.White, duration)
        };

    private IBrush ResolveBrush(object color, string fallback)
    {
        if (color is string resourceKey)
            return LegacyResourceResolver.Brush(this, resourceKey, fallback);
        if (color is Color directColor)
            return new SolidColorBrush(directColor);
        return new SolidColorBrush(Color.Parse(fallback));
    }
}
