using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Codecs;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Test;

[TestClass]
public class EncodingDetectorTest
{
    [TestMethod]
    public void TestEncoding()
    {
        var utf8 = Encoding.UTF8.GetBytes("Hi, There!");
        Assert.AreEqual(EncodingDetector.DetectEncoding(utf8), Encoding.UTF8);
        utf8 = Encoding.UTF8.GetBytes("棍斤拷烫烫烫");
        Assert.AreEqual(EncodingDetector.DetectEncoding(utf8), Encoding.UTF8);
        var gb = Encoding.GetEncoding("gb2312").GetBytes("你好世界");
        Assert.AreEqual(EncodingDetector.DetectEncoding(gb), Encoding.GetEncoding("gb2312"));
        // var gbnew = Encoding.GetEncoding("GB18030").GetBytes("你好世界");
        // Assert.AreEqual(EncodingDetector.DetectEncoding(gbnew), Encoding.GetEncoding("GB18030"));
        byte[] nonEncode = [0xfe, 0x5f, 0xa1];
        Assert.AreEqual(Encoding.Default, EncodingDetector.DetectEncoding(nonEncode));
    }

    [TestMethod]
    public void StreamPositionPastEndReturnsDefaultDetectionAndRestoresPosition()
    {
        using var stream = new MemoryStream("text"u8.ToArray());
        stream.Position = stream.Length + 10;

        var encoding = EncodingDetector.DetectEncoding(stream);

        Assert.AreEqual(Encoding.UTF8, encoding);
        Assert.AreEqual(stream.Length + 10, stream.Position);
    }
}
