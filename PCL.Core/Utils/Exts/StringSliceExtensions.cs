using System;

namespace PCL.Core.Utils.Exts;

/// <summary>
///     字符串切片扩展。
/// </summary>
public static class StringSliceExtensions
{
    extension(string value)
    {
        public string BeforeFirst(string marker, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(value);
            var index = string.IsNullOrEmpty(marker)
                ? -1
                : value.IndexOf(
                    marker,
                    ignoreCase
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
            return index >= 0
                ? value[..index]
                : value;
        }

        public string BeforeLast(string marker, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(value);
            var index = string.IsNullOrEmpty(marker)
                ? -1
                : value.LastIndexOf(
                    marker,
                    ignoreCase
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
            return index >= 0
                ? value[..index]
                : value;
        }

        public string AfterFirst(string marker, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(value);
            var index = string.IsNullOrEmpty(marker)
                ? -1
                : value.IndexOf(
                    marker,
                    ignoreCase
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
            return index >= 0
                ? value[(index + marker.Length)..]
                : value;
        }

        public string AfterLast(string marker, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(value);
            var index = string.IsNullOrEmpty(marker)
                ? -1
                : value.LastIndexOf(
                    marker,
                    ignoreCase
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);
            return index >= 0
                ? value[(index + marker.Length)..]
                : value;
        }

        public string Between(string after, string before, bool ignoreCase = false)
        {
            ArgumentNullException.ThrowIfNull(value);

            var comparison = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var start = string.IsNullOrEmpty(after)
                ? -1
                : value.LastIndexOf(after, comparison);
            start = start >= 0
                ? start + after.Length
                : 0;

            var end = string.IsNullOrEmpty(before)
                ? -1
                : value.IndexOf(before, start, comparison);
            if (end >= 0) return value[start..end];
            return start > 0
                ? value[start..]
                : value;
        }
    }
}