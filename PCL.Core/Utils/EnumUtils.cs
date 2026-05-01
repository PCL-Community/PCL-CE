using System;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.Utils;

public static class EnumUtils
{
    public static T ParseToEnum<T>(string input) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (T)(object)0;
        }
        else if (int.TryParse(input, out var numericValue))
        {
            return (T)(object)numericValue;
        }
        else
        {
            return Enum.Parse<T>(input, true);
        }
    }

    public static string GetEnumName<T>(T content) where T : struct, Enum
    {
        return Enum.GetName(content.GetType(), content)!;
    }

    public static bool TryGetEnumName<T>(T content, [NotNullWhen(true)] out string? result) where T : struct, Enum
    {
        var str = Enum.GetName(content.GetType(), content);
        result = str;
        return str is not null;
    }
}