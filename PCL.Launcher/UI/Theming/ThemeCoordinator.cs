using System.Diagnostics;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.UI.Theme;

namespace PCL.UI.Theming;

/// <summary>
///     把 PCL 主题状态接到 Avalonia 应用层：配色模式决定 <see cref="Application.RequestedThemeVariant" />，
///     主题色变化后由 ThemeService 重写应用资源，样式层通过 DynamicResource 自动跟随。
///     必须在 PCL.Core 生命周期启动前调用 <see cref="Install" />，
///     否则会错过 ThemeService 在 WindowCreating 阶段发出的首次主题事件。
/// </summary>
public static class ThemeCoordinator
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        ThemeService.ColorModeChanged += _OnColorModeChanged;
        ThemeService.ColorThemeChanged += _OnColorThemeChanged;
        if (Lifecycle.CurrentState >= LifecycleState.WindowCreating)
            _Apply(ThemeService.IsDarkMode);
        Trace.WriteLine("[Theme] 主题协调器已安装");
    }

    private static void _OnColorModeChanged(bool isDarkMode, ColorTheme theme)
    {
        _Apply(isDarkMode);
    }

    private static void _OnColorThemeChanged(ColorTheme theme)
    {
        _Apply(ThemeService.IsDarkMode);
    }

    private static void _Apply(bool isDarkMode)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => _Apply(isDarkMode));
            return;
        }

        if (Application.Current is not { } app) return;

        var variant = isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        if (app.RequestedThemeVariant != variant) app.RequestedThemeVariant = variant;

        Trace.WriteLine(
            $"[Theme] 已应用主题：variant={(isDarkMode ? "Dark" : "Light")}, "
            + $"theme={ThemeService.CurrentTheme}");
    }
}