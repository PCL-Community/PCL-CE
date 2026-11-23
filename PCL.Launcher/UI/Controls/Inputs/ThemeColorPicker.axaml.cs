using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using FluentValidation;
using PCL.Core.App.Localization;
using PCL.Core.UI.Theming;

namespace PCL.UI.Controls;

/// <summary>主题自定义颜色编辑器：格式选择、数值输入、SV 面板、色相条与顶栏渐变条。 只负责编辑颜色与顶栏渐变幅度，不直接读写配置。</summary>
public partial class ThemeColorPicker : UserControl
{
    public delegate void ColorPickedEventHandler(ThemeColorPicker sender, Color color);

    public delegate void HueSpreadPickedEventHandler(ThemeColorPicker sender, int hueSpread);

    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<ThemeColorPicker, Color>(
            nameof(Color),
            Colors.DodgerBlue,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> HueSpreadProperty =
        AvaloniaProperty.Register<ThemeColorPicker, int>(nameof(HueSpread));

    private readonly InlineValidator<string> _hexValidator;
    private readonly MyTextBox[] _numberFields;
    private readonly TextBlock[] _numberLabels;

    private ThemeColorFormat _format = ThemeColorFormat.Hsv;
    private bool _isDraggingGradient;
    private bool _isDraggingHue;
    private bool _isDraggingSv;
    private bool _isUpdating;
    private double _lastHue = 240d;
    private double _renderedHue = double.NaN;

    public ThemeColorPicker()
    {
        InitializeComponent();

        _numberFields = [TextFirst, TextSecond, TextThird];
        _numberLabels = [LabelFirst, LabelSecond, LabelThird];

        foreach (var t in _numberFields)
        {
            var validator = new InlineValidator<string>();
            validator
                .RuleFor(text => text)
                .Must(IsNumberTextValid)
                .WithMessage(_ => Lang.Text("Setup.Ui.Theme.Custom.InvalidColor"));
            t.ValidateRules = [validator];
            t.ValidatedTextChanged += (_, _) => OnNumberFieldChanged();
            t.LostFocus += (_, _) => NormalizeFields();
            t.KeyDown += OnFieldKeyDown;
        }

        _hexValidator = new InlineValidator<string>();
        _hexValidator
            .RuleFor(text => text)
            .Must(IsHexTextValid)
            .WithMessage(_ => Lang.Text("Setup.Ui.Theme.Custom.InvalidColor"));
        TextHex.ValidateRules = [_hexValidator];
        TextHex.ValidatedTextChanged += (_, _) => OnHexFieldChanged();
        TextHex.LostFocus += (_, _) => NormalizeFields();
        TextHex.KeyDown += OnFieldKeyDown;

        ComboFormat.SelectionChanged += OnFormatSelectionChanged;

        SvHost.SetLocalizedValue(ToolTip.TipProperty, "Setup.Ui.Theme.Custom.Sv");
        HueHost.SetLocalizedValue(ToolTip.TipProperty, "Setup.Ui.Theme.Custom.Hue");
        GradientHost.SetLocalizedValue(
            ToolTip.TipProperty,
            "Setup.Ui.Theme.Custom.TitleBarGradient");

        SvHost.PointerPressed += OnSvPointerPressed;
        SvHost.PointerMoved += OnSvPointerMoved;
        SvHost.PointerReleased += OnSvPointerReleased;
        SvHost.PointerCaptureLost += OnSvPointerCaptureLost;

        HueHost.PointerPressed += OnHuePointerPressed;
        HueHost.PointerMoved += OnHuePointerMoved;
        HueHost.PointerReleased += OnHuePointerReleased;
        HueHost.PointerCaptureLost += OnHuePointerCaptureLost;

        GradientHost.PointerPressed += OnGradientPointerPressed;
        GradientHost.PointerMoved += OnGradientPointerMoved;
        GradientHost.PointerReleased += OnGradientPointerReleased;
        GradientHost.PointerCaptureLost += OnGradientPointerCaptureLost;

        SvHost.SizeChanged += (_, _) =>
        {
            var hsv = Color.ToHsv();
            PositionSvCursor(hsv.S, hsv.V);
        };
        HueHost.SizeChanged += (_, _) => PositionHueCursor(_lastHue);
        GradientHost.SizeChanged += (_, _) => PositionGradientCursor();

        Loaded += (_, _) =>
        {
            UpdateFormatLayout();
            UpdateColorVisuals(Color);
            UpdateFieldsFromColor(Color);
        };
        Unloaded += (_, _) =>
        {
            _isDraggingSv = false;
            _isDraggingHue = false;
            _isDraggingGradient = false;
            HideValueTip();
        };
    }

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetCurrentValue(ColorProperty, value);
    }

    public int HueSpread
    {
        get => Math.Clamp(GetValue(HueSpreadProperty), -90, 90);
        set => SetCurrentValue(HueSpreadProperty, Math.Clamp(value, -90, 90));
    }

    /// <summary>用户通过输入框、SV 面板或色相条改变了颜色。</summary>
    public event ColorPickedEventHandler? ColorPicked;

    /// <summary>用户拖动了顶栏渐变条。</summary>
    public event HueSpreadPickedEventHandler? HueSpreadPicked;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HueSpreadProperty)
        {
            if (GradientHost is not null)
                UpdateGradientVisual(_lastHue);
            return;
        }

        if (change.Property != ColorProperty
            || change.NewValue is not Color color
            || TextFirst is null)
            return;

        UpdateColorVisuals(color);
        if (!_isUpdating)
            UpdateFieldsFromColor(color);
    }

    private void OnFormatSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var format = ComboFormat.SelectedIndex switch
        {
            1 => ThemeColorFormat.Rgb,
            2 => ThemeColorFormat.Hex,
            _ => ThemeColorFormat.Hsv
        };
        if (_format == format)
            return;

        _format = format;
        UpdateFormatLayout();
        UpdateFieldsFromColor(Color);

        foreach (var field in _numberFields)
            field.Validate();
        TextHex.Validate();
    }

    private void OnNumberFieldChanged()
    {
        if (_isUpdating || _format == ThemeColorFormat.Hex)
            return;

        if (!_numberFields.Any(field => field.IsFocused))
            return;

        if (TryParseNumberFields(out var color))
            ApplyColorFromInput(color);
    }

    private void OnHexFieldChanged()
    {
        if (_isUpdating || _format != ThemeColorFormat.Hex)
            return;

        if (!TextHex.IsFocused)
            return;

        if (ThemeColorText.TryParseHex(TextHex.Text ?? string.Empty, out var color))
            ApplyColorFromInput(color);
    }

    private void ApplyColorFromInput(Color color)
    {
        _isUpdating = true;
        try
        {
            SetCurrentValue(ColorProperty, color);
            UpdateColorVisuals(color);
        }
        finally
        {
            _isUpdating = false;
        }

        ColorPicked?.Invoke(this, color);
    }

    private bool IsNumberTextValid(string? text)
    {
        if (_format == ThemeColorFormat.Hex) return true;
        return ThemeColorText.TryParseNumber(text, out _);
    }

    private bool IsHexTextValid(string? text)
    {
        return _format != ThemeColorFormat.Hex
            || ThemeColorText.TryParseHex(text ?? string.Empty, out _);
    }

    private bool TryParseNumberFields(out Color color)
    {
        switch (_format)
        {
            case ThemeColorFormat.Hsv:
            {
                if (!ThemeColorText.TryParseHsvFields(
                        TextFirst.Text,
                        TextSecond.Text,
                        TextThird.Text,
                        out color,
                        out var hue))
                    return false;

                _lastHue = hue;
                return true;
            }

            case ThemeColorFormat.Rgb:
            {
                return ThemeColorText.TryParseRgbFields(
                    TextFirst.Text,
                    TextSecond.Text,
                    TextThird.Text,
                    out color);
            }

            default:
            {
                color = default;
                return false;
            }
        }
    }

    private void NormalizeFields()
    {
        if (_isUpdating)
            return;

        if (_format == ThemeColorFormat.Hex)
        {
            if (ThemeColorText.TryParseHex(TextHex.Text ?? string.Empty, out _))
                UpdateFieldsFromColor(Color);
            return;
        }

        if (TryParseNumberFields(out _))
            UpdateFieldsFromColor(Color);
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NormalizeFields();
        e.Handled = true;
    }

    private void UpdateFormatLayout()
    {
        var isHex = _format == ThemeColorFormat.Hex;
        ThreeFieldsHost.IsVisible = !isHex;
        HexFieldsHost.IsVisible = isHex;

        if (isHex)
        {
            TextHex.HintText = "RRGGBB";
            return;
        }

        var isHsv = _format == ThemeColorFormat.Hsv;
        _numberLabels[0].Text = isHsv ? "H" : "R";
        _numberLabels[1].Text = isHsv ? "S" : "G";
        _numberLabels[2].Text = isHsv ? "V" : "B";

        _numberFields[0].HintText = isHsv ? "0-360" : "0-255";
        _numberFields[1].HintText = isHsv ? "0-100" : "0-255";
        _numberFields[2].HintText = isHsv ? "0-100" : "0-255";
    }

    private void UpdateFieldsFromColor(Color color)
    {
        _isUpdating = true;
        try
        {
            switch (_format)
            {
                case ThemeColorFormat.Hex:
                {
                    TextHex.Text = ThemeColorText.FormatHex(color);
                    TextHex.ForceShowAsSuccess();
                    return;
                }

                case ThemeColorFormat.Hsv:
                {
                    var hsv = color.ToHsv();
                    var (formattedHue, saturation, value) = ThemeColorText.FormatHsv(color);
                    var hue = hsv.S > 0.0001d && hsv.V > 0.0001d
                        ? formattedHue
                        : (int)Math.Round(_lastHue);
                    if (hue >= 360) hue = 0;
                    _numberFields[0].Text = hue.ToString(CultureInfo.InvariantCulture);
                    _numberFields[1].Text = saturation.ToString(CultureInfo.InvariantCulture);
                    _numberFields[2].Text = value.ToString(CultureInfo.InvariantCulture);
                    break;
                }

                case ThemeColorFormat.Rgb:
                default:
                {
                    var (red, green, blue) = ThemeColorText.FormatRgb(color);
                    _numberFields[0].Text = red.ToString(CultureInfo.InvariantCulture);
                    _numberFields[1].Text = green.ToString(CultureInfo.InvariantCulture);
                    _numberFields[2].Text = blue.ToString(CultureInfo.InvariantCulture);
                    break;
                }
            }

            foreach (var field in _numberFields)
                field.ForceShowAsSuccess();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateColorVisuals(Color color)
    {
        var hsv = color.ToHsv();
        if (hsv is { S: > 0.0001d, V: > 0.0001d })
            _lastHue = hsv.H;

        UpdateHueVisual(_lastHue);
        UpdateGradientVisual(_lastHue);
        PositionSvCursor(hsv.S, hsv.V);
    }

    private void UpdateHueVisual(double hue)
    {
        if (double.IsNaN(_renderedHue) || Math.Abs(hue - _renderedHue) > 0.01d)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative)
            };
            brush.GradientStops.Add(new GradientStop(Colors.White, 0d));
            brush.GradientStops.Add(new GradientStop(new HsvColor(1d, hue, 1d, 1d).ToRgb(), 1d));
            SvHueLayer.Background = brush;
            _renderedHue = hue;
        }

        PositionHueCursor(hue);
    }

    private void UpdateGradientVisual(double hue)
    {
        var hsv = Color.ToHsv();
        var spread = HueSpread;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(
            new GradientStop(
                new HsvColor(1d, NormalizeHue(hue - spread), hsv.S, hsv.V).ToRgb(),
                0d));
        brush.GradientStops.Add(new GradientStop(Color, 0.5d));
        brush.GradientStops.Add(
            new GradientStop(
                new HsvColor(1d, NormalizeHue(hue + spread), hsv.S, hsv.V).ToRgb(),
                1d));

        GradientLayer.Background = brush;
        PositionGradientCursor();
    }

    private static double NormalizeHue(double hue)
    {
        hue %= 360d;
        return hue < 0d ? hue + 360d : hue;
    }

    private void PositionSvCursor(double saturation, double value)
    {
        var width = SvHost.Bounds.Width;
        var height = SvHost.Bounds.Height;
        if (width <= 0d || height <= 0d)
            return;

        var x = Math.Clamp(
            saturation * width - SvCursor.Width / 2d,
            0d,
            Math.Max(0d, width - SvCursor.Width));
        var y = Math.Clamp(
            (1d - value) * height - SvCursor.Height / 2d,
            0d,
            Math.Max(0d, height - SvCursor.Height));
        Canvas.SetLeft(SvCursor, x);
        Canvas.SetTop(SvCursor, y);
    }

    private void PositionHueCursor(double hue)
    {
        var width = HueHost.Bounds.Width;
        if (width <= 0d)
            return;

        var x = Math.Clamp(
            hue / 360d * width - HueCursor.Width / 2d,
            0d,
            Math.Max(0d, width - HueCursor.Width));
        Canvas.SetLeft(HueCursor, x);
        Canvas.SetTop(HueCursor, 0d);
    }

    private void PositionGradientCursor()
    {
        var width = GradientHost.Bounds.Width;
        if (width <= 0d)
            return;

        var ratio = (HueSpread + 90d) / 180d;
        var x = Math.Clamp(
            ratio * width - GradientCursor.Width / 2d,
            0d,
            Math.Max(0d, width - GradientCursor.Width));
        Canvas.SetLeft(GradientCursor, x);
        Canvas.SetTop(GradientCursor, 0d);
    }

    private void OnSvPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SvHost).Properties.IsLeftButtonPressed)
            return;

        _isDraggingSv = true;
        e.Pointer.Capture(SvHost);
        UpdateSvFromPoint(e.GetPosition(SvHost), e.GetPosition(this));
        e.Handled = true;
    }

    private void OnSvPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingSv)
            UpdateSvFromPoint(e.GetPosition(SvHost), e.GetPosition(this));
    }

    private void OnSvPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingSv) return;
        _isDraggingSv = false;
        HideValueTip();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnSvPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDraggingSv = false;
        HideValueTip();
    }

    private void UpdateSvFromPoint(Point point, Point displayPoint)
    {
        var width = SvHost.Bounds.Width;
        var height = SvHost.Bounds.Height;
        if (width <= 0d || height <= 0d)
            return;

        var saturation = Math.Clamp(point.X / width, 0d, 1d);
        var value = 1d - Math.Clamp(point.Y / height, 0d, 1d);
        ApplyColorFromInteraction(new HsvColor(1d, _lastHue, saturation, value).ToRgb());
        ShowValueTip($"S {saturation * 100d:0}% · V {value * 100d:0}%", displayPoint);
    }

    private void OnHuePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(HueHost).Properties.IsLeftButtonPressed)
            return;

        _isDraggingHue = true;
        e.Pointer.Capture(HueHost);
        UpdateHueFromPoint(e.GetPosition(HueHost), e.GetPosition(this));
        e.Handled = true;
    }

    private void OnHuePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingHue)
            UpdateHueFromPoint(e.GetPosition(HueHost), e.GetPosition(this));
    }

    private void OnHuePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingHue)
            return;

        _isDraggingHue = false;
        HideValueTip();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnHuePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDraggingHue = false;
        HideValueTip();
    }

    private void UpdateHueFromPoint(Point point, Point displayPoint)
    {
        var width = HueHost.Bounds.Width;
        if (width <= 0d)
            return;

        var hue = Math.Clamp(point.X / width, 0d, 1d) * 360d;
        if (hue >= 360d) hue = 0d;

        _lastHue = hue;
        UpdateHueVisual(hue);

        var hsv = Color.ToHsv();
        ApplyColorFromInteraction(new HsvColor(1d, hue, hsv.S, hsv.V).ToRgb());
        var displayHue = (int)Math.Round(hue);
        if (displayHue >= 360) displayHue = 0;
        ShowValueTip($"H {displayHue}°", displayPoint);
    }

    private void OnGradientPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(GradientHost).Properties.IsLeftButtonPressed)
            return;

        _isDraggingGradient = true;
        e.Pointer.Capture(GradientHost);
        UpdateGradientFromPoint(e.GetPosition(GradientHost), e.GetPosition(this));
        e.Handled = true;
    }

    private void OnGradientPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingGradient)
            UpdateGradientFromPoint(e.GetPosition(GradientHost), e.GetPosition(this));
    }

    private void OnGradientPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingGradient)
            return;

        _isDraggingGradient = false;
        HideValueTip();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnGradientPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDraggingGradient = false;
        HideValueTip();
    }

    private void UpdateGradientFromPoint(Point point, Point displayPoint)
    {
        var width = GradientHost.Bounds.Width;
        if (width <= 0d)
            return;

        var ratio = Math.Clamp(point.X / width, 0d, 1d);
        var hueSpread = (int)Math.Round(ratio * 180d - 90d);
        SetCurrentValue(HueSpreadProperty, hueSpread);
        ShowValueTip(FormatHueSpread(hueSpread), displayPoint);
        HueSpreadPicked?.Invoke(this, hueSpread);
    }

    private static string FormatHueSpread(int hueSpread)
    {
        return hueSpread.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "°";
    }

    private void ShowValueTip(string text, Point point)
    {
        ValueTipText.Text = text;
        ValueTip.IsVisible = true;
        ValueTip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0d || height <= 0d) return;

        var size = ValueTip.DesiredSize;
        var left = Math.Clamp(point.X - size.Width / 2d, 0d, Math.Max(0d, width - size.Width));
        var top = point.Y - size.Height - 12d;
        if (top < 0d)
            top = Math.Min(Math.Max(0d, height - size.Height), point.Y + 16d);

        Canvas.SetLeft(ValueTip, left);
        Canvas.SetTop(ValueTip, top);
    }

    private void HideValueTip()
    {
        ValueTip.IsVisible = false;
    }

    private void ApplyColorFromInteraction(Color color)
    {
        _isUpdating = true;
        try
        {
            SetCurrentValue(ColorProperty, color);
            UpdateColorVisuals(color);
        }
        finally
        {
            _isUpdating = false;
        }

        UpdateFieldsFromColor(color);
        ColorPicked?.Invoke(this, color);
    }
}