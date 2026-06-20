// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PCL.Core.Utils.Encryption;

public interface IEncryptionProvider
{
    public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key);
    public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key);

    public static bool IsSupported { get; }
}
