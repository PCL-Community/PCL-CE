using System;
using System.Text;

namespace PCL.Core.Utils.Exts;

public static class ByteExtension
{
    extension(ReadOnlySpan<byte> bytes)
    {
        public string ToHexString()
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}