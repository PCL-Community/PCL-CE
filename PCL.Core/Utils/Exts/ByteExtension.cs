using System;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Utils.Exts;

public static class ByteExtension
{
    extension(ReadOnlySpan<byte> bytes)
    {
        public string ToHexString() => Convert.ToHexStringLower(bytes);
        public string FromByteToB64() => Convert.ToBase64String(bytes);
        public string FromBytesToB64UrlSafe() => bytes.FromByteToB64().FromB64ToB64UrlSafe();
    }
}
