using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Humanizer;
using PCL.Core.App.IoC;
using PCL.Core.IO;

namespace PCL.Core.App.Localization;

/// <summary>
///     <p>
///         本地化文本与展示格式访问辅助。
///     </p>
///     <p>
///         <see cref="Lang" /> 用于代码中读取本地化文本资源，以及按照当前展示区域性格式化文本参数、日期时间和数值。
///         它面向 C# 代码侧调用；XAML 中的静态文本优先使用 <c>DynamicResource</c>，
///         XAML 绑定值的格式化优先使用 <see cref="LocalizationFormatConverter" />。
///     </p>
///     <p>
///         文本资源来自当前应用的资源字典。正常运行时优先通过
///         <see cref="Application.Current" /> 查找资源；在应用生命周期早期或测试环境中，
///         会回退到 <see cref="Lifecycle.CurrentApplication" /> 进行一次安全查找。
///     </p>
///     <p>
///         该类中的格式化方法使用 <see cref="CultureInfo.CurrentCulture" />，
///         因此会跟随 <see cref="LocalizationService" /> 当前设置的展示格式区域性。
///         它们只适合生成展示给用户看的文本，不应用于配置文件、日志、协议、缓存键、文件名等
///         需要稳定格式的场景；这些场景应显式使用 <see cref="CultureInfo.InvariantCulture" />。
///     </p>
/// </summary>
public static class Lang
{
    private static readonly HashSet<string> _UnrestrictedTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "China Standard Time"
    };

    private static readonly HashSet<string> _UnrestrictedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN"
    };

    /// <summary>
    ///     当前代码侧本地化格式化使用的展示区域性。
    /// </summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentCulture;

    /// <summary>
    ///     当前功能是否不受限制。
    /// </summary>
    public static bool IsFeaturesUnrestricted =>
        Config.Debug.AllowRestrictedFeature ||
        (
            _UnrestrictedTimeZones.Contains(TimeZoneInfo.Local.Id) &&
            (_UnrestrictedCultures.Contains(CultureInfo.CurrentCulture.Name) ||
             _UnrestrictedCultures.Contains(CultureInfo.CurrentUICulture.Name))
        );
    
    /// <summary>
    ///     当前展示区域性 <see cref="Culture"/> 是否为 <c>zh-CN</c>。
    /// </summary>
    public static bool IsChineseMainland =>
        Culture.Name == "zh-CN";

    /// <summary>
    ///     同步展示区域性。该方法由 <see cref="LocalizationService" /> 在语言或展示格式变化时调用。
    /// </summary>
    /// <param name="culture">
    ///     新的展示区域性。
    /// </param>
    internal static void SyncCulture(CultureInfo culture)
    {
        Culture = culture;
    }

    /// <summary>
    ///     获取指定资源键对应的本地化文本。
    /// </summary>
    /// <param name="key">
    ///     资源键。不能为空、空字符串或空白字符串。
    /// </param>
    /// <returns>
    ///     找到资源时返回本地化文本；
    ///     未找到资源时，调试构建返回 <c>!key!</c>，发布构建返回 <paramref name="key" /> 本身。
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     <paramref name="key" /> 为空、空字符串或空白字符串。
    /// </exception>
    public static string Text(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Application.Current?.TryFindResource(key) is string text) return text;
        if (_LifecycleSafeFindResource(key) is string fallbackText) return fallbackText;

#if DEBUG
        return $"!{key}!";
#else
        return key;
#endif
    }

    /// <summary>
    ///     获取指定资源键对应的本地化格式文本，并使用当前展示区域性格式化参数。
    ///     资源文本应使用标准 .NET 复合格式字符串，例如 <c>{0}</c>、<c>{1:N2}</c>。
    ///     该方法适合代码中生成用户可见句子，例如提示、说明、状态文本。
    /// </summary>
    /// <param name="key">
    ///     资源键。不能为空、空字符串或空白字符串。
    /// </param>
    /// <param name="args">
    ///     用于填充资源文本中格式占位符的参数。
    /// </param>
    /// <returns>
    ///     使用当前展示区域性格式化后的本地化文本。
    /// </returns>
    public static string Text(string key, params object?[] args)
    {
        return string.Format(Culture, Text(key), args);
    }

    /// <summary>
    ///     <p>使用当前展示区域性格式化日期时间。</p>
    /// </summary>
    /// <param name="value">
    ///     要格式化的日期时间。
    /// </param>
    /// <param name="format">
    ///     标准或自定义日期时间格式字符串，默认使用 <c>G</c>。
    /// </param>
    /// <returns>
    ///     使用当前展示区域性格式化后的日期时间文本。
    /// </returns>
    public static string Date(DateTime value, string format = "G")
    {
        return value.ToString(format, Culture);
    }

    /// <summary>
    ///     <p>使用当前展示区域性格式化日期时间。</p>
    /// </summary>
    /// <param name="value">
    ///     要格式化的日期时间。
    /// </param>
    /// <param name="format">
    ///     标准或自定义日期时间格式字符串，默认使用 <c>G</c>。
    /// </param>
    /// <returns>
    ///     使用当前展示区域性格式化后的日期时间文本。
    /// </returns>
    public static string Date(DateTimeOffset value, string format = "G")
    {
        return value.ToString(format, Culture);
    }

    /// <summary>
    ///     使用当前展示区域性格式化时间间隔。
    /// </summary>
    /// <param name="span">
    ///     要格式化的时间间隔。负值表示过去，正值表示未来。
    /// </param>
    /// <param name="precision">
    ///     要显示的时间单位数量。例如值为 <c>1</c> 时仅显示最大的一项时间单位，值为 <c>2</c> 时显示两项时间单位。
    /// </param>
    /// <param name="addAffixes">
    ///     如果为 <see langword="true" />，会根据 <paramref name="span" /> 的正负为文本添加“过去”或“未来”语义；
    ///     如果为 <see langword="false" />，仅返回格式化后的时间间隔本身。
    /// </param>
    /// <param name="maxUnit">
    ///     允许显示的最大时间单位。
    /// </param>
    /// <param name="minUnit">
    ///     允许显示的最小时间单位。
    /// </param>
    /// <returns>
    ///     使用当前展示区域性格式化后的时间间隔文本；当 <paramref name="addAffixes" /> 为 <see langword="true" /> 时，
    ///     返回值会包含相对于当前时间的过去或未来语义。
    /// </returns>
    public static string TimeSpan(
        TimeSpan span, int precision = 2, bool addAffixes = true,
        TimeUnit maxUnit = TimeUnit.Year, TimeUnit minUnit = TimeUnit.Hour)
    {
        var isPast = span.TotalMilliseconds < 0;
        if (isPast) span = span.Negate();

        var text = span.Humanize(
            precision,
            maxUnit: maxUnit,
            minUnit: minUnit,
            culture: Culture);

        return addAffixes
            ? Text(isPast ? "Common.Format.TimeSpan.Past" : "Common.Format.TimeSpan.Future", text)
            : text;
    }

    /// <summary>
    ///     使用当前展示区域性格式化数值。
    /// </summary>
    /// <typeparam name="T">
    ///     实现 <see cref="IFormattable" /> 的数值或可格式化类型。
    /// </typeparam>
    /// <param name="value">
    ///     要格式化的值。
    /// </param>
    /// <param name="format">
    ///     标准或自定义格式字符串。为 <see langword="null" /> 时使用类型默认格式。
    /// </param>
    /// <returns>
    ///     使用当前展示区域性格式化后的文本。
    /// </returns>
    public static string Number<T>(T value, string? format = null) where T : IFormattable
    {
        return value.ToString(format, Culture);
    }

    /// <summary>
    ///     使用当前展示区域性格式化紧凑数值，例如下载量。
    /// </summary>
    /// <param name="value">
    ///     要格式化的整数。
    /// </param>
    /// <returns>
    ///     按当前展示区域性格式化后的紧凑数值文本，例如 <c>11M</c>、<c>2 万</c>。
    /// </returns>
    public static string CompactNumber(long value)
    {
        var absValue = Math.Abs((double)value);
        var sign = value < 0 ? -1 : 1;

        // 使用 4 位进位
        if (_IsEastAsianCulture(Culture.Name))
            return absValue switch
            {
                > 1_000_000_000_000d => Text("Common.Format.Number.Digit3",
                    Number(sign * absValue / 1_000_000_000_000d, "N2")),
                > 100_000_000d => Text("Common.Format.Number.Digit2",
                    Number(sign * absValue / 100_000_000d, "N2")),
                > 100_000d => Text("Common.Format.Number.Digit1",
                    Number(sign * Math.Round(absValue / 10_000d), "N0")),
                _ => Number(value, "N0")
            };

        // 使用 3 位进位
        return absValue switch
        {
            > 1_000_000_000d => Text("Common.Format.Number.Digit3",
                Number(sign * absValue / 1_000_000_000d, "N2")),
            > 1_000_000d => Text("Common.Format.Number.Digit2",
                Number(sign * absValue / 1_000_000d, "N2")),
            > 10_000d => Text("Common.Format.Number.Digit1",
                Number(sign * Math.Round(absValue / 1_000d), "N0")),
            _ => Number(value, "N0")
        };
    }

    /// <summary>
    ///     使用当前展示区域性格式化文件大小，例如 <c>1.28 MB</c>。
    /// </summary>
    /// <param name="length">字节数</param>
    /// <param name="startUnit">起始单位索引，0 为 B</param>
    /// <returns>格式化后的文件大小文本。</returns>
    public static string FileSize(long length, int startUnit = 0)
    {
        return ByteStream.GetReadableLength(length, startUnit, Culture);
    }

    private static bool _IsEastAsianCulture(string cultureName)
    {
        return cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
               cultureName is "ja-JP" or "ko-KR" or "lzh";
    }

    private static object? _LifecycleSafeFindResource(string key)
    {
        try
        {
            return Lifecycle.CurrentApplication.TryFindResource(key);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }
}