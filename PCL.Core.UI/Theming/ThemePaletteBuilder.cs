using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL.Core.UI.Theming;

/// <summary>按指定模式与主题生成色阶画刷，不修改全局主题资源。</summary>
public static class ThemePaletteBuilder
{
    private static readonly string[] LadderSuffixes = ["1", "2", "3", "4", "5", "6", "7", "8"];

    public static ThemePalette Build(bool isDarkMode, ColorTheme theme)
    {
        var tone = isDarkMode ? ThemeService.ToneProfiles.Dark : ThemeService.ToneProfiles.Light;
        var args = ThemeService.GetThemeArgs(theme, isDarkMode);
        var resources = ThemeService.CalculateColorResources(tone, args);
        var map = resources.ToDictionary(resource => resource.Suffix, StringComparer.Ordinal);

        return new ThemePalette
        {
            Ladder =
            [
                .. LadderSuffixes.Select(IBrush (suffix) =>
                    new SolidColorBrush(map[suffix].ToAvaloniaColor()))
            ]
        };
    }
}