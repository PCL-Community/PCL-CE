using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

public class BlurBorder : Border
{
    private const string Module = "UI";
    
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<BlurBorder, double>(nameof(BlurRadius), 16d);
    
    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }
    
    private bool _configSubscribed;
    private ConfigObserver? _enabledObserver;
    private ConfigObserver? _radiusObserver;
    
    public BlurBorder()
    {
        _UpdateBlurEffect();
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _SubscribeConfig();
        _ApplyBlurSettings();
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _UnsubscribeConfig();
        base.OnDetachedFromVisualTree(e);
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == BlurRadiusProperty)
            _UpdateBlurEffect();
    }

    private void _ApplyBlurSettings()
    {
        try
        {
            var blur = Config.Preference.Blur;

            SetCurrentValue(BlurRadiusProperty, blur.IsEnabled ? Math.Max(blur.Radius, 0d) : 0d);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "[BlurBorder] 应用背景模糊配置失败");
        }
    }
    
    private void _UpdateBlurEffect()
    {
        BackdropEffect = BlurRadius > 0 ? new BlurEffect { Radius = BlurRadius } : null;
    }

    private void _SubscribeConfig()
    {
        if (_configSubscribed)
            return;

        try
        {
            _enabledObserver = new ConfigObserver(ConfigEvent.Update, _ => _ApplyBlurSettings());

            _radiusObserver = new ConfigObserver(ConfigEvent.Update, _ => _ApplyBlurSettings());

            Config.Preference.Blur.IsEnabledConfig.Observe(_enabledObserver);
            Config.Preference.Blur.RadiusConfig.Observe(_radiusObserver);

            _configSubscribed = true;
        }
        catch (Exception ex)
        {
            _UnsubscribeConfig();

            LogWrapper.Debug(ex, Module, "[BlurBorder] 订阅背景模糊配置失败");
        }
    }

    private void _UnsubscribeConfig()
    {
        try
        {
            if (_enabledObserver is not null) 
                Config.Preference.Blur.IsEnabledConfig.Unobserve(_enabledObserver);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "[BlurBorder] 取消模糊开关配置订阅失败");
        }

        try
        {
            if (_radiusObserver is not null)
                Config.Preference.Blur.RadiusConfig.Unobserve(_radiusObserver);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "[BlurBorder] 取消模糊半径配置订阅失败");
        }

        _enabledObserver = null;
        _radiusObserver = null;
        _configSubscribed = false;
    }
}