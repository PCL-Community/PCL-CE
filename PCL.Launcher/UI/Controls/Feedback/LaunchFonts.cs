using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;

namespace PCL.UI.Controls;

/// <summary>
///     启动器字体链：内置的 <c>PCL English</c> 负责拉丁字形，其余字体按当前 UI 语言
///     回退到系统字体；用户指定字体时插到首位并保留语言回退。
/// </summary>
internal static class LaunchFonts
{
    private const string EmbeddedFontFamily = "PCL English";

    private const string EmbeddedFontUri = "avares://PCL.Launcher/Assets/Fonts#PCL English";

    /// <summary>当前 UI 语言对应的默认字体链。</summary>
    public static FontFamily Default { get; } = Build(null);

    /// <summary>按当前 UI 语言重新生成默认字体链，供语言切换后刷新。</summary>
    public static FontFamily ForCurrentLanguage()
    {
        return Build(null);
    }

    /// <summary>
    ///     MOTD 使用的字体：配置了 <c>Config.Preference.MotdFont</c> 时使用该字体，
    ///     否则回到默认字体链。
    /// </summary>
    public static FontFamily ResolveMotdFont()
    {
        var configured = ConfigService.IsInitialized ? Config.Preference.MotdFont : string.Empty;
        return string.IsNullOrWhiteSpace(configured)
            ? ForCurrentLanguage()
            : Parse([configured], ForCurrentLanguage());
    }

    /// <summary>生成字体链；<paramref name="customFontName" /> 为空时使用语言默认值。</summary>
    public static FontFamily Build(string? customFontName)
    {
        var families = GetDefaultFamilies(LocalizationService.CurrentLanguage.FontProfile).ToList();

        if (!string.IsNullOrWhiteSpace(customFontName))
        {
            families.RemoveAll(family =>
                string.Equals(family, EmbeddedFontFamily, StringComparison.OrdinalIgnoreCase));
            families.Insert(0, customFontName.Trim());
        }

        return Parse(families, FontFamily.Default);
    }

    private static FontFamily Parse(IEnumerable<string> families, FontFamily fallback)
    {
        var names = families
            .Where(family => !string.IsNullOrWhiteSpace(family))
            .Select(family => string.Equals(
                family,
                EmbeddedFontFamily,
                StringComparison.OrdinalIgnoreCase)
                ? EmbeddedFontUri
                : family)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        try
        {
            return FontFamily.Parse(string.Join(", ", names));
        }
        catch
        {
            return fallback;
        }
    }

    private static IReadOnlyList<string> GetDefaultFamilies(LocalizationFontProfile profile)
    {
        return profile switch
        {
            LocalizationFontProfile.English =>
                [EmbeddedFontFamily, "Segoe UI", "Microsoft YaHei UI"],

            LocalizationFontProfile.SimplifiedChinese =>
                [EmbeddedFontFamily, "Microsoft YaHei UI", "Segoe UI"],

            LocalizationFontProfile.TraditionalChinese =>
                [EmbeddedFontFamily, "Microsoft JhengHei UI", "Microsoft YaHei UI", "Segoe UI"],

            LocalizationFontProfile.Japanese =>
                [EmbeddedFontFamily, "Yu Gothic UI", "Microsoft YaHei UI", "Segoe UI"],

            LocalizationFontProfile.Korean =>
                [EmbeddedFontFamily, "Malgun Gothic", "Microsoft YaHei UI", "Segoe UI"],

            _ => ["Segoe UI", "Microsoft YaHei UI"]
        };
    }
}