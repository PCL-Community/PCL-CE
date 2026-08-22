using System.Text.RegularExpressions;
using PCL.Core.Logging;

namespace PCL;

internal static class CrashRegex
{
    public static string? First(
        string text,
        string pattern,
        RegexOptions options = RegexOptions.None)
    {
        try
        {
            var value = Regex.Match(text, pattern, options).Value;
            return string.IsNullOrEmpty(value)
                ? null
                : value;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则匹配第一项出错");
            return null;
        }
    }

    public static List<string> All(
        string text,
        string pattern,
        RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.Matches(text, pattern, options)
                .Select(match => match.Value)
                .ToList();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则匹配全部项出错");
            return [];
        }
    }

    public static bool IsMatch(
        string text,
        string pattern,
        RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.IsMatch(text, pattern, options);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则检查出错");
            return false;
        }
    }
}