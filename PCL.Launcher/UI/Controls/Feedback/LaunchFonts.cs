using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;

namespace PCL.UI.Controls;

/// <summary>
///     启动器字体链：内置的 <c>PCL English</c> 负责拉丁字形，其余字体按当前 UI 语言回退到系统字体。
///     用户指定字体时插到首位，由 <see cref="InstallGlobalFont" /> 写入应用级资源。
/// </summary>
internal static class LaunchFonts
{
    public const string ApplicationFontKey = "LaunchFontFamily";

    private const string EmbeddedFontFamily = "PCL English";

    private const string EmbeddedFontUri = "avares://PCL.Launcher/Assets/Fonts#PCL English";

    /// <summary>当前 UI 语言对应的默认字体链。</summary>
    public static FontFamily Default { get; } = Build(null);

    public static void InstallGlobalFont()
    {
        if (Application.Current is not { } application) return;

        // 语言切换可能来自后台线程，资源改写需要留在 UI 线程。
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() =>
                application.Resources[ApplicationFontKey] = ForCurrentLanguage());
            return;
        }

        application.Resources[ApplicationFontKey] = ForCurrentLanguage();
    }

    /// <summary>按当前 UI 语言重新生成默认字体链，供语言切换后刷新。</summary>
    public static FontFamily ForCurrentLanguage()
    {
        return Build(null);
    }

    public static FontFamily BuildRepresentativeFontFamily(LocalizationLanguage language)
    {
        return Parse(GetDefaultFamilies(language.FontProfile), FontFamily.Default);
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
                [EmbeddedFontFamily, "Microsoft YaHei UI", "Segoe UI", "Noto Sans", "DejaVu Sans"],

            LocalizationFontProfile.SimplifiedChinese =>
            [
                EmbeddedFontFamily, "Microsoft YaHei UI", "Segoe UI", "Noto Sans CJK SC",
                "Source Han Sans SC", "WenQuanYi Micro Hei", "Noto Sans SC"
            ],

            LocalizationFontProfile.TraditionalChinese =>
            [
                EmbeddedFontFamily, "Microsoft JhengHei UI", "Microsoft YaHei UI", "Segoe UI",
                "Noto Sans CJK TC", "Noto Sans TC"
            ],

            LocalizationFontProfile.Japanese =>
            [
                EmbeddedFontFamily, "Yu Gothic UI", "Microsoft YaHei UI", "Segoe UI",
                "Noto Sans CJK JP", "Noto Sans JP"
            ],

            LocalizationFontProfile.Korean =>
            [
                EmbeddedFontFamily, "Malgun Gothic", "Microsoft YaHei UI", "Segoe UI",
                "Noto Sans CJK KR", "Noto Sans KR"
            ],

            _ => ["Segoe UI", "Microsoft YaHei UI", "Noto Sans", "DejaVu Sans"]
        };
    }
}