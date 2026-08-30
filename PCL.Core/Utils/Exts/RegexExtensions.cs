using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Core.Logging;

namespace PCL.Core.Utils.Exts;

/// <summary>
///     正则表达式扩展。
/// </summary>
public static class RegexExtensions
{
    extension(string value)
    {
        public string RegexReplace(
            string pattern,
            string replacement,
            RegexOptions options = RegexOptions.None)
        {
            ArgumentNullException.ThrowIfNull(value);
            return Regex.Replace(value, pattern, replacement, options);
        }

        public string RegexReplaceEach(
            string pattern,
            MatchEvaluator evaluator,
            RegexOptions options = RegexOptions.None)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(evaluator);
            return Regex.Replace(value, pattern, evaluator, options);
        }
    }

    extension(string? value)
    {
        public List<string> RegexSearch(
            string pattern,
            RegexOptions options = RegexOptions.None)
        {
            return value is null
                ? []
                : Regex.Matches(value, pattern, options)
                    .Select(match => match.Value)
                    .ToList();
        }

        public string? RegexSeek(
            string pattern,
            RegexOptions options = RegexOptions.None)
        {
            if (value is null) return null;
            var result = Regex.Match(value, pattern, options).Value;
            return string.IsNullOrEmpty(result)
                ? null
                : result;
        }

        public string? RegexSeek(Regex regex)
        {
            ArgumentNullException.ThrowIfNull(regex);
            if (value is null) return null;
            var result = regex.Match(value).Value;
            return string.IsNullOrEmpty(result)
                ? null
                : result;
        }

        public bool RegexCheck(
            string pattern,
            RegexOptions options = RegexOptions.None)
        {
            if (value is null) return false;
            try
            {
                return Regex.IsMatch(value, pattern, options);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "正则检查出错");
                return false;
            }
        }
    }
}