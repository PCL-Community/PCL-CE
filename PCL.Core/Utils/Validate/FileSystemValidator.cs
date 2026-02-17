using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentValidation;

namespace PCL.Core.Utils.Validate;

public abstract class FileSystemValidator : AbstractValidator<string>
{
    protected static string? CheckInvalidStrings(string input, string[] extraInvalidStrings)
    {
        if (string.IsNullOrEmpty(input)) return null;

        var invalidStrings = Path.GetInvalidFileNameChars().Select(c => c.ToString());
        invalidStrings = invalidStrings.Concat(extraInvalidStrings);
        
        // 找出字符串中包含的非法字符
        var found = invalidStrings
            .Where(input.Contains)
            .Distinct()
            .ToArray();
        
        return found.Length != 0 ? string.Join("  ", found) : null;
    }

    protected static string? CheckReservedWord(string input, string[] extraReservedWords)
    {
        if (string.IsNullOrEmpty(input)) return null;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(input);
        
        IEnumerable<string> reserved = 
        [
            "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7",
            "COM8", "COM9", "COM¹", "COM²", "COM³", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7",
            "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
        ];
        reserved = reserved.Concat(extraReservedWords);

        // 找出匹配的保留字
        var matched = reserved.FirstOrDefault(r => r.Equals(nameWithoutExtension));

        return matched ?? null;
    }
}