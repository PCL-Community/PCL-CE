using System;
using System.Text;

namespace PCL.Core.Utils.Exts;

public static class ByteExtension
{
    extension(ReadOnlySpan<byte> bytes)
    {
        public string ToHexString() => Convert.ToHexString(bytes).ToLower();
        public string FromByteToB64() => Convert.ToBase64String(bytes);
        public string FromBytesToB64UrlSafe() => bytes.FromByteToB64().FromB64ToB64UrlSafe();
    }
}
