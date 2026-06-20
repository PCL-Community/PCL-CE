// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.Utils;

namespace PCL.Core.Test;

[TestClass]
public sealed class VarIntHelperTests
{
    [TestMethod]
    [DataRow(0UL)]
    [DataRow(1UL)]
    [DataRow(127UL)]
    [DataRow(128UL)]
    [DataRow(uint.MaxValue)]
    [DataRow(ulong.MaxValue)]
    public void SpanRoundTripPreservesValue(ulong value)
    {
        Span<byte> buffer = stackalloc byte[10];

        Assert.IsTrue(VarIntHelper.TryEncode(value, buffer, out var written));
        Assert.AreEqual(value, VarIntHelper.Decode(buffer[..written], out var read));
        Assert.AreEqual(written, read);
    }

    [TestMethod]
    public void DestinationTooSmallReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[1];

        Assert.IsFalse(VarIntHelper.TryEncode(128, buffer, out var written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public async Task StreamReadDecodesValue()
    {
        await using var stream = new MemoryStream(VarIntHelper.Encode(ulong.MaxValue));

        Assert.AreEqual(ulong.MaxValue, await VarIntHelper.ReadFromStreamAsync(stream));
    }
}
