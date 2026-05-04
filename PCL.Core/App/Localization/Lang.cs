using System;
using System.Globalization;
using System.Windows;
using PCL.Core.App.IoC;

namespace PCL.Core.App.Localization;

/// <summary>
///     本地化文本访问辅助。
/// </summary>
public static class Lang
{
    /// <summary>
    ///     获取本地化文本。未找到资源时，调试构建返回 !key!，发布构建返回 key 本身。
    /// </summary>
    /// <param name="key">资源键。</param>
    public static string Text(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Application.Current?.TryFindResource(key) is string text) return text;
        if (LifecycleSafeFindResource(key) is string fallbackText) return fallbackText;

#if DEBUG
        return $"!{key}!";
#else
        return key;
#endif
    }

    /// <summary>
    ///     获取本地化格式文本，并使用当前展示区域性格式化参数。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <param name="args">格式化参数。</param>
    public static string Text(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Text(key), args);
    }

    /// <summary>
    ///     使用当前展示区域性格式化日期时间。
    /// </summary>
    public static string Date(DateTime value, string format = "G")
    {
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    ///     使用当前展示区域性格式化数值。
    /// </summary>
    public static string Number<T>(T value, string? format = null) where T : IFormattable
    {
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    private static object? LifecycleSafeFindResource(string key)
    {
        try
        {
            return Lifecycle.CurrentApplication?.TryFindResource(key);
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