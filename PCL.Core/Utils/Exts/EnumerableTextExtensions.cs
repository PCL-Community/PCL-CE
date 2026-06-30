using System;
using System.Collections;
using System.Text;

namespace PCL.Core.Utils.Exts;

/// <summary>
///     Text helpers for enumerable values.
/// </summary>
public static class EnumerableTextExtensions
{
    /// <summary>
    ///     Joins an enumerable into a string. Null elements are skipped, matching string builder based launcher formatting.
    /// </summary>
    public static string Join(this IEnumerable source, string separator)
    {
        ArgumentNullException.ThrowIfNull(source);
        separator ??= string.Empty;

        var builder = new StringBuilder();
        var isFirst = true;
        foreach (var item in source)
        {
            if (isFirst)
                isFirst = false;
            else
                builder.Append(separator);

            if (item is not null)
                builder.Append(item);
        }

        return builder.ToString();
    }
}