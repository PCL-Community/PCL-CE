using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

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

    private static readonly Lock PreviewFallbackLock = new();
    private static string? _previewFallbackChain;
    private static LocalizationFontProfile? _previewFallbackProfile;

    /// <summary>当前 UI 语言对应的默认字体链。</summary>
    public static FontFamily Default { get; } = Build(null);

    /// <summary>
    ///     语言页横幅字体。Windows 使用 Bahnschrift，其他平台只使用内嵌字体，
    ///     避免 Avalonia 在缺少该字体的系统上创建 GlyphTypeface 失败。
    /// </summary>
    public static FontFamily BannerFont { get; } = OperatingSystem.IsWindows()
        ? Build("Bahnschrift")
        : new FontFamily(EmbeddedFontUri);

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
        var configured = ConfigService.IsInitialized ? Config.Preference.Font : string.Empty;
        return Build(configured);
    }

    /// <summary>
    ///     为字体选择器生成安全的预览字体链。首选实际字体名，缺失时回退到内置字体和当前语言默认字体。
    ///     不能直接使用 <see cref="FontManager.SystemFonts" /> 返回的 FontFamily：它带有
    ///     <c>fonts:SystemFonts</c> Key，字体缺失时不会触发回退，而会在文本测量阶段抛出异常。
    /// </summary>
    internal static FontFamily BuildPreviewFont(string familyName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(familyName)
                || familyName.Contains(',')
                || familyName.Contains('#'))
                return Default;

            var fallback = GetPreviewFallbackChain();
            return string.IsNullOrEmpty(fallback)
                ? Default
                : new FontFamily($"{familyName}, {fallback}");
        }
        catch
        {
            return Default;
        }
    }

    /// <summary>
    ///     预览字体的回退链。按语言缓存，避免每个下拉项都验证一遍系统字体。
    /// </summary>
    private static string GetPreviewFallbackChain()
    {
        var profile = LocalizationService.CurrentLanguage.FontProfile;
        if (_previewFallbackChain is not null && _previewFallbackProfile == profile)
            return _previewFallbackChain;

        lock (PreviewFallbackLock)
        {
            if (_previewFallbackChain is not null && _previewFallbackProfile == profile)
                return _previewFallbackChain;

            var families = GetDefaultFamilies(profile)
                .Select(family => string.Equals(
                    family,
                    EmbeddedFontFamily,
                    StringComparison.OrdinalIgnoreCase)
                    ? EmbeddedFontUri
                    : family)
                .Where(family =>
                    family.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                    || _IsFontAvailable(family))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _previewFallbackChain = string.Join(", ", families);
            _previewFallbackProfile = profile;
            return _previewFallbackChain;
        }
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
            : Build(configured);
    }

    /// <summary>生成字体链；<paramref name="customFontName" /> 为空时使用语言默认值。</summary>
    public static FontFamily Build(string? customFontName)
    {
        var families = GetDefaultFamilies(LocalizationService.CurrentLanguage.FontProfile).ToList();

        if (!string.IsNullOrWhiteSpace(customFontName))
        {
            var custom = customFontName.Trim();
            if (_IsFontAvailable(custom))
                families.Insert(0, custom);
            else
                LogWrapper.Warn("UI", $"配置的字体不可用，已回退到默认字体：{custom}");
        }

        return Parse(families, FontFamily.Default);
    }

    /// <summary>
    ///     判断字体是否能创建 GlyphTypeface；缺失的系统字体应提前回退，
    ///     否则 Avalonia 可能在控件测量阶段抛出“Could not create glyphTypeface”。
    /// </summary>
    private static bool _IsFontAvailable(string family)
    {
        if (family.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsFontAvailable(new FontFamily(family));
    }

    /// <summary>
    ///     判断字体是否能创建 GlyphTypeface。字体选择器与默认字体链必须共用这套判断，
    ///     否则 fontconfig 能列出、Skia 无法加载的字体仍会在控件测量阶段抛异常。
    /// </summary>
    internal static bool IsFontAvailable(FontFamily font)
    {
        if (Application.Current is null)
            return true;

        try
        {
            return font.Key is not null
                ? FontManager.Current.TryGetGlyphTypeface(new Typeface(font), out _)
                : FontManager.Current.SystemFonts.TryGetGlyphTypeface(
                    font.Name,
                    FontStyle.Normal,
                    FontWeight.Normal,
                    FontStretch.Normal,
                    out _);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static FontFamily Parse(IEnumerable<string> families, FontFamily fallback)
    {
        var names = families
            .Where(family => !string.IsNullOrWhiteSpace(family))
            .Where(family =>
                string.Equals(family, EmbeddedFontFamily, StringComparison.OrdinalIgnoreCase)
                || _IsFontAvailable(family))
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
            [
                EmbeddedFontFamily,
                "Microsoft YaHei UI",
                "Segoe UI",
                "Helvetica Neue",
                "Noto Sans",
                "DejaVu Sans"
            ],

            LocalizationFontProfile.SimplifiedChinese =>
            [
                EmbeddedFontFamily,
                "Microsoft YaHei UI",
                "Segoe UI",
                "PingFang SC",
                "Hiragino Sans GB",
                "Heiti SC",
                "Noto Sans CJK SC",
                "Source Han Sans SC",
                "WenQuanYi Micro Hei",
                "Noto Sans SC"
            ],

            LocalizationFontProfile.TraditionalChinese =>
            [
                EmbeddedFontFamily,
                "Microsoft JhengHei UI",
                "Microsoft YaHei UI",
                "Segoe UI",
                "PingFang TC",
                "Heiti TC",
                "Noto Sans CJK TC",
                "Noto Sans TC"
            ],

            LocalizationFontProfile.Japanese =>
            [
                EmbeddedFontFamily,
                "Yu Gothic UI",
                "Microsoft YaHei UI",
                "Segoe UI",
                "Hiragino Sans",
                "Hiragino Kaku Gothic ProN",
                "Noto Sans CJK JP",
                "Noto Sans JP"
            ],

            LocalizationFontProfile.Korean =>
            [
                EmbeddedFontFamily,
                "Malgun Gothic",
                "Microsoft YaHei UI",
                "Segoe UI",
                "Apple SD Gothic Neo",
                "Noto Sans CJK KR",
                "Noto Sans KR"
            ],

            _ =>
            [
                EmbeddedFontFamily,
                "Segoe UI",
                "Microsoft YaHei UI",
                "PingFang SC",
                "Noto Sans CJK SC",
                "Noto Sans",
                "DejaVu Sans"
            ]
        };
    }
}