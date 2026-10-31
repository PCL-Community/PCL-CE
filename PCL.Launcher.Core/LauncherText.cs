using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace PCL.Text;

/// <summary>
///     业务层使用的文本与数值解析工具，替代原 <c>Common</c> 中的同名纯工具方法。
/// </summary>
public static class LauncherText
{
    /// <summary>
    ///     不会报错的 Val。如果输入有误，返回 0。
    /// </summary>
    public static double Val(object str)
    {
        try
        {
            return str is "&" ? 0d : Conversion.Val(str);
        }
        catch
        {
            return 0d;
        }
    }

    extension(string str)
    {
        /// <summary>支持可变大小写判断的 Contains。</summary>
        public bool ContainsF(string subStr, bool ignoreCase = false)
        {
            return str.Contains(
                subStr,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>获取字符串中的第一个正则匹配项，若无匹配则返回 <c>null</c>。</summary>
        public string? RegexSeek(
            string regex,
            RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = Regex.Match(str, regex, options).Value;
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>支持可变大小写判断的 StartsWith。</summary>
        public bool StartsWithF(string prefix, bool ignoreCase = false)
        {
            return str.StartsWith(
                prefix,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>支持可变大小写判断的 EndsWith。</summary>
        public bool EndsWithF(string suffix, bool ignoreCase = false)
        {
            return str.EndsWith(
                suffix,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>获取字符串中的第一个正则匹配项，若无匹配则返回 <c>null</c>。</summary>
        public string? RegexSeek(
            Regex regex,
            RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = regex.Match(str, (int)options).Value;
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>检查字符串是否匹配正则。</summary>
        public bool RegexCheck(Regex regex)
        {
            try
            {
                return regex.IsMatch(str);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>区分大小写的快速 IndexOf。</summary>
        public int IndexOfF(string subStr, bool ignoreCase = false)
        {
            return str.IndexOf(
                subStr,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>区分大小写的快速 LastIndexOf。</summary>
        public int LastIndexOfF(string subStr, bool ignoreCase = false)
        {
            return str.LastIndexOf(
                subStr,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>获取在子字符串最后一次出现之后的部分；未找到时返回原字符串。</summary>
        public string AfterLast(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.LastIndexOfF(text, ignoreCase);
            return pos >= 0 ? str[(pos + text.Length)..] : str;
        }

        /// <summary>将首字母转为大写。</summary>
        public string Capitalize()
        {
            return string.IsNullOrEmpty(str)
                ? str
                : char.ToUpperInvariant(str[0]) + str[1..];
        }
    }

    /// <summary>按布尔比较器稳定排序。</summary>
    public static List<T> SortByPredicate<T>(IEnumerable<T> values, Func<T, T, bool> rule)
    {
        var result = new List<T>();
        foreach (var item in values)
        {
            var index = result.Count;
            while (index > 0 && !rule(result[index - 1], item)) index -= 1;
            result.Insert(index, item);
        }

        return result;
    }

    extension(string str)
    {
        /// <summary>是否为纯 ASCII 字符串。</summary>
        public bool IsAscii()
        {
            return str.All(c => c < 128);
        }

        /// <summary>获取在子字符串最后一次出现之前的部分；未找到时返回原字符串。</summary>
        public string BeforeLast(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.LastIndexOfF(text, ignoreCase);
            return pos >= 0 ? str[..pos] : str;
        }

        /// <summary>获取在子字符串第一次出现之后的部分；未找到时返回原字符串。</summary>
        public string AfterFirst(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.IndexOfF(text, ignoreCase);
            return pos >= 0 ? str[(pos + text.Length)..] : str;
        }
    }

    extension<T>(IEnumerable<T> values)
    {
        /// <summary>用分隔符连接集合。</summary>
        public string Join(string separator)
        {
            return string.Join(separator, values);
        }
    }

    extension<T>(IEnumerable<T> source)
    {
        /// <summary>按自定义相等比较去重，保留首次出现的元素。</summary>
        public List<T> Distinct(Func<T, T, bool> comparer)
        {
            var result = new List<T>();
            foreach (var item in source)
                if (!result.Any(existing => comparer(existing, item)))
                    result.Add(item);
            return result;
        }
    }

    extension(string str)
    {
        /// <summary>获取在子字符串第一次出现之前的部分；未找到时不裁切。</summary>
        public string BeforeFirst(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text)
                ? -1
                : str.IndexOf(
                    text,
                    ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            return pos >= 0 ? str[..pos] : str;
        }

        /// <summary>检查字符串是否匹配正则。</summary>
        public bool RegexCheck(
            string regex,
            RegexOptions options = RegexOptions.None)
        {
            try
            {
                return Regex.IsMatch(str, regex, options);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>搜索字符串中的所有正则匹配项。</summary>
        public List<string> RegexSearch(Regex regex)
        {
            try
            {
                var result = new List<string>();
                foreach (Match item in regex.Matches(str)) result.Add(item.Value);
                return result;
            }
            catch
            {
                return [];
            }
        }

        /// <summary>搜索字符串中的所有正则匹配项。</summary>
        public List<string> RegexSearch(
            string regex,
            RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = new List<string>();
                foreach (Match item in new Regex(regex, options).Matches(str))
                    result.Add(item.Value);
                return result;
            }
            catch
            {
                return [];
            }
        }
    }
}