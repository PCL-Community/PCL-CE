using System;
using System.IO;

namespace PCL.Core.IO;

public class ByteStream(Stream stream)
{
    public long Length => stream.Length;

    public string GetReadableLength() => GetReadableLength(this.Length);
    
    public static string GetReadableLength(long length)
    {
        string[] unit = ["B", "KB", "MB", "GB", "TB", "PB"];
        var displayCount = length * 100;
        var displayUnit = 0;
        while (displayCount >= 102400)
        {
            displayCount >>= 10;
            displayUnit++;
        }

        if (displayUnit > unit.Length)
            throw new IndexOutOfRangeException("Why there is no enough unit to show :(");
        var displayText = displayCount.ToString();
        displayText = displayText.Insert(displayText.Length - 2, ".");
        return $"{displayText} {unit[displayUnit]}";
    }
}