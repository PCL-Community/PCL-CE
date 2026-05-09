using System;
using System.Globalization;
using System.Windows;
using PCL.Core.App.IoC;

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
        return string.Format(CultureInfo.CurrentCulture, Text(key), args);
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
        return value.ToString(format, CultureInfo.CurrentCulture);
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
        return value.ToString(format, CultureInfo.CurrentCulture);
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