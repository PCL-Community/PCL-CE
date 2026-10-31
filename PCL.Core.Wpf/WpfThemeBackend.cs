using System.Windows;
using System.Windows.Media;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF 资源字典的主题后端。</summary>
public sealed class WpfThemeBackend : IThemeBackend
{
    public void SetColorResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue,
        bool asBrush)
    {
        var color = Color.FromScRgb(alpha, red, green, blue);
        object value = asBrush ? new SolidColorBrush(color) : color;

        if (Application.Current is { } app) app.Resources[key] = value;
        else Lifecycle.CurrentApplication?.SetResource(key, value);
    }
}