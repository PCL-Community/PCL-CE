namespace PCL.Core.App.Localization;

/// <summary>
///     表示一个受支持的 UI 语言。
/// </summary>
/// <param name="Code">语言配置值。</param>
/// <param name="NativeName">语言的本地名称。</param>
/// <param name="CultureName">用于 <see cref="System.Globalization.CultureInfo" /> 的区域性名称。</param>
/// <param name="FontProfile"> 语言对应的 UI 字体策略。
/// </param>
public sealed record LocalizationLanguage(
    string Code,
    string NativeName,
    string CultureName,
    LocalizationFontProfile FontProfile);