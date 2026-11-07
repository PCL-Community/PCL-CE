using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

public enum BlurKernelType
{
    Gaussian,

    Box
}

public enum BlurRenderingBias
{
    Performance,

    Quality
}

/// <summary>
///     用提高背景不透明度的方式近似磨砂效果；Avalonia 没有可采样背景像素的控件级模糊 API。
///     <see cref="BlurRadius" /> 与 <see cref="FrostOpacity" /> 取自背景模糊配置，关闭时退化为普通 <see cref="Border" />
///     。
/// </summary>
public class BlurBorder : Border
{
    private const string Module = "UI";

    private static bool _degradationLogged;
    private static bool _configFailureLogged;

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<BlurBorder, double>(nameof(BlurRadius), 16d);

    public static readonly StyledProperty<double> BlurSamplingRateProperty =
        AvaloniaProperty.Register<BlurBorder, double>(nameof(BlurSamplingRate), 0.9d);

    public static readonly StyledProperty<int> MaxDepthProperty =
        AvaloniaProperty.Register<BlurBorder, int>(nameof(MaxDepth));

    public static readonly StyledProperty<BlurKernelType> BlurKernelTypeProperty =
        AvaloniaProperty.Register<BlurBorder, BlurKernelType>(
            nameof(BlurKernelType));

    public static readonly StyledProperty<BlurRenderingBias> BlurRenderingBiasProperty =
        AvaloniaProperty.Register<BlurBorder, BlurRenderingBias>(
            nameof(BlurRenderingBias));

    public static readonly StyledProperty<double> FrostOpacityProperty =
        AvaloniaProperty.Register<BlurBorder, double>(nameof(FrostOpacity));

    private bool _applyingFrost;
    private bool _configSubscribed;
    private ConfigObserver? _enabledObserver;
    private bool _frostApplied;
    private ConfigObserver? _radiusObserver;
    private IBrush? _sourceBackground;

    public BlurBorder()
    {
        if (!_degradationLogged)
        {
            _degradationLogged = true;
            LogWrapper.Debug(
                Module,
                "[BlurBorder] 无控件级背景模糊 API，背景模糊设置开启时改用提高纯色背景不透明度的方式近似");
        }

        ApplyBlurSettings();
    }

    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public double BlurSamplingRate
    {
        get => GetValue(BlurSamplingRateProperty);
        set => SetValue(BlurSamplingRateProperty, Math.Clamp(value, 0.1d, 1d));
    }

    public int MaxDepth
    {
        get => GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    public BlurKernelType BlurKernelType
    {
        get => GetValue(BlurKernelTypeProperty);
        set => SetValue(BlurKernelTypeProperty, value);
    }

    public BlurRenderingBias BlurRenderingBias
    {
        get => GetValue(BlurRenderingBiasProperty);
        set => SetValue(BlurRenderingBiasProperty, value);
    }

    public double FrostOpacity
    {
        get => GetValue(FrostOpacityProperty);
        set => SetValue(FrostOpacityProperty, Math.Clamp(value, 0d, 1d));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _SubscribeConfig();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _UnsubscribeConfig();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_applyingFrost) return;

        if (change.Property == BackgroundProperty)
        {
            _sourceBackground = change.NewValue as IBrush;
            _ApplyFrost();
        }
        else if (change.Property == BlurRadiusProperty
                 || change.Property == FrostOpacityProperty)
        {
            _ApplyFrost();
        }
    }

    private void ApplyBlurSettings()
    {
        try
        {
            var blur = Config.Preference.Blur;
            if (!blur.IsEnabled)
            {
                SetCurrentValue(BlurRadiusProperty, 0d);
                SetCurrentValue(FrostOpacityProperty, 0d);
                return;
            }

            SetCurrentValue(BlurRadiusProperty, Math.Max(blur.Radius, 0d));
            SetCurrentValue(FrostOpacityProperty, 1d);
        }
        catch (Exception ex)
        {
            if (_configFailureLogged) return;
            _configFailureLogged = true;
            LogWrapper.Debug(ex, Module, "[BlurBorder] 读取背景模糊配置失败，磨砂近似保持关闭");
        }
    }

    private void _SubscribeConfig()
    {
        if (_configSubscribed)
            return;

        try
        {
            _enabledObserver = new ConfigObserver(ConfigEvent.Update, _ => ApplyBlurSettings());
            _radiusObserver = new ConfigObserver(ConfigEvent.Update, _ => ApplyBlurSettings());
            Config.Preference.Blur.IsEnabledConfig.Observe(_enabledObserver);
            Config.Preference.Blur.RadiusConfig.Observe(_radiusObserver);
            _configSubscribed = true;
            ApplyBlurSettings();
        }
        catch (Exception ex)
        {
            _enabledObserver = null;
            _radiusObserver = null;
            if (_configFailureLogged) return;
            _configFailureLogged = true;
            LogWrapper.Debug(ex, Module, "[BlurBorder] 订阅背景模糊配置失败，磨砂近似保持关闭");
        }
    }

    private void _UnsubscribeConfig()
    {
        if (!_configSubscribed)
            return;

        try
        {
            if (_enabledObserver is not null)
                Config.Preference.Blur.IsEnabledConfig.Unobserve(_enabledObserver);
            if (_radiusObserver is not null)
                Config.Preference.Blur.RadiusConfig.Unobserve(_radiusObserver);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "[BlurBorder] 取消背景模糊配置订阅失败");
        }
        finally
        {
            _enabledObserver = null;
            _radiusObserver = null;
            _configSubscribed = false;
        }
    }

    private void _ApplyFrost()
    {
        var source = _sourceBackground ?? Background;
        var opacity = FrostOpacity;
        if (opacity <= 0d || BlurRadius <= 0d || source is not ISolidColorBrush solid)
        {
            if (!_frostApplied) return;

            _frostApplied = false;
            _applyingFrost = true;
            try
            {
                SetCurrentValue(BackgroundProperty, _sourceBackground);
            }
            finally
            {
                _applyingFrost = false;
            }

            return;
        }

        var radiusFactor = Math.Clamp(BlurRadius / 40d, 0d, 1d);
        var strength = Math.Clamp(opacity * (0.4d + 0.6d * radiusFactor), 0d, 1d);
        var alpha = (byte)Math.Round(solid.Color.A + (255d - solid.Color.A) * strength);

        _frostApplied = true;
        _applyingFrost = true;
        try
        {
            SetCurrentValue(
                BackgroundProperty,
                new SolidColorBrush(
                    Color.FromArgb(alpha, solid.Color.R, solid.Color.G, solid.Color.B)));
        }
        finally
        {
            _applyingFrost = false;
        }
    }
}