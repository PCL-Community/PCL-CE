using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Test;

[TestClass]
public class EncodingDetectorTest
{
    [TestInitialize]
    public void SetUp()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [TestMethod]
    public void TestEncoding()
    {
        var utf8 = "Hi, There!"u8.ToArray();
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8));

        utf8 = [.. "棍斤拷烫烫烫"u8];
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8));

        var gb = Encoding.GetEncoding("gb2312").GetBytes("你好世界");
        Assert.AreEqual(Encoding.GetEncoding("GB18030"), EncodingDetector.DetectEncoding(gb));

        var gb18030 = Encoding.GetEncoding("GB18030").GetBytes("你好世界");
        Assert.AreEqual(Encoding.GetEncoding("GB18030"), EncodingDetector.DetectEncoding(gb18030));

        byte[] nonEncode = [0xfe, 0x5f, 0xa1];
        Assert.AreEqual(Encoding.Default, EncodingDetector.DetectEncoding(nonEncode));
    }

    [TestMethod]
    public void DetectsBomEncodingsCorrectlyEvenWhenFileIsLongerThanFourBytes()
    {
        // UTF-8 with BOM (>= 4 bytes)
        byte[] utf8WithBom =
            [0xef, 0xbb, 0xbf, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o'];
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8WithBom));

        // UTF-16 LE with BOM (>= 4 bytes)
        byte[] utf16LeWithBom = [0xff, 0xfe, (byte)'H', 0x00, (byte)'i', 0x00];
        Assert.AreEqual(Encoding.Unicode, EncodingDetector.DetectEncoding(utf16LeWithBom));

        // UTF-16 BE with BOM (>= 4 bytes)
        byte[] utf16BeWithBom = [0xfe, 0xff, 0x00, (byte)'H', 0x00, (byte)'i'];
        Assert.AreEqual(Encoding.BigEndianUnicode, EncodingDetector.DetectEncoding(utf16BeWithBom));

        // UTF-32 LE with BOM (>= 4 bytes)
        byte[] utf32LeWithBom = [0xff, 0xfe, 0x00, 0x00, (byte)'H', 0x00, 0x00, 0x00];
        Assert.AreEqual(Encoding.UTF32, EncodingDetector.DetectEncoding(utf32LeWithBom));

        // UTF-32 BE with BOM (>= 4 bytes)
        byte[] utf32BeWithBom = [0x00, 0x00, 0xfe, 0xff, 0x00, 0x00, 0x00, (byte)'H'];
        Assert.AreEqual(
            Encoding.GetEncoding("utf-32BE"),
            EncodingDetector.DetectEncoding(utf32BeWithBom));
    }

    [TestMethod]
    public void DetectsUtf8Across1024ByteBoundary()
    {
        // 构造一个超过 1024 字节且在 1024 字节处跨越多字节 UTF-8 字符的字符串
        var sb = new StringBuilder();
        while (Encoding.UTF8.GetByteCount(sb.ToString()) < 1022) sb.Append('a');
        sb.Append("中文测试字符串，长度超过1024字节以测试边界");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        Assert.IsGreaterThan(1024, bytes.Length);
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(bytes));
    }

    [TestMethod]
    public void DetectEncodingWithReadFromBeginHandlesNonZeroPosition()
    {
        var bytes = "HelloWorld"u8.ToArray();
        using var stream = new MemoryStream(bytes);
        stream.Position = 5;

        var detected = EncodingDetector.DetectEncoding(stream, true);
        Assert.AreEqual(Encoding.UTF8, detected);
        Assert.AreEqual(5, stream.Position);
    }

    [TestMethod]
    public void DecodeBytesFallsBackToGb18030WhenAsciiPrefixPrecedesChineseGb18030Bytes()
    {
        // 构造前 1500 字节为 ASCII，后续包含 GB18030 中文的字节序列（典型如大型 mcmod.info）
        var prefix = new string(' ', 1500);
        var fullText = prefix + "\"description\": \"这是一个旧版模组的中文描述内容\"";
        var gbBytes = Encodings.GB18030.GetBytes(fullText);

        var decoded = EncodingUtils.DecodeBytes(gbBytes);
        Assert.AreEqual(fullText, decoded);
    }
}