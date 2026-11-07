using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Test;

[TestClass]
public class EncodingDetectorTest
{
    [TestMethod]
    public void TestEncoding()
    {
        var utf8 = (byte[])[.. "Hi, There!"u8];
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8));
        utf8 = [.. "棍斤拷烫烫烫"u8];
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8));

        var gb = Encoding.GetEncoding("gb2312").GetBytes("你好世界");
        Assert.AreEqual(
            Encoding.Default.EncodingName,
            EncodingDetector.DetectEncoding(gb).EncodingName);

        byte[] nonEncode = [0xfe, 0x5f, 0xa1];
        Assert.AreEqual(
            Encoding.Default.EncodingName,
            EncodingDetector.DetectEncoding(nonEncode).EncodingName);

        Assert.AreEqual(
            Encoding.Unicode,
            EncodingDetector.DetectEncoding([0xff, 0xfe, 0x41, 0x00]));
        Assert.AreEqual(
            Encoding.BigEndianUnicode,
            EncodingDetector.DetectEncoding([0xfe, 0xff, 0x00, 0x41]));
        Assert.AreEqual(Encoding.UTF32, EncodingDetector.DetectEncoding([0xff, 0xfe, 0x00, 0x00]));
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding([0xef, 0xbb, 0xbf, 0x41]));
    }
}