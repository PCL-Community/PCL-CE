using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.Test;
[TestClass]
public class EncodingDetectorTest
{
    [ClassInitialize]
    public static void Initialize(TestContext context)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [TestMethod]
    public void TestEncoding()
    {
        var utf8 = Encoding.UTF8.GetBytes("Hi, There!");
        Assert.AreEqual(EncodingDetector.DetectEncoding(utf8), Encoding.UTF8);
        utf8 = Encoding.UTF8.GetBytes("棍斤拷烫烫烫");
        Assert.AreEqual(EncodingDetector.DetectEncoding(utf8), Encoding.UTF8);
        var gb = Encoding.GetEncoding("gb2312").GetBytes("你好世界");
        Assert.AreEqual(EncodingDetector.DetectEncoding(gb), Encoding.GetEncoding("GB18030"));
        // var gbnew = Encoding.GetEncoding("GB18030").GetBytes("你好世界");
        // Assert.AreEqual(EncodingDetector.DetectEncoding(gbnew), Encoding.GetEncoding("GB18030"));
        byte[] nonEncode = [0xfe, 0x5f, 0xa1];
        Assert.AreEqual(Encoding.GetEncoding("GB18030"), EncodingDetector.DetectEncoding(nonEncode));
    }

    [TestMethod]
    public void TestGbkAfterAsciiHead()
    {
        var gb = Encoding.GetEncoding("GB18030").GetBytes(new string('a', 10000) + "你好世界");
        Assert.AreEqual(Encoding.GetEncoding("GB18030"), EncodingDetector.DetectEncoding(gb));
    }

    [TestMethod]
    public void TestUtf8AcrossReadBoundary()
    {
        for (var shift = 0; shift < 3; shift++)
        {
            var utf8 = Encoding.UTF8.GetBytes(new string('a', shift) + new string('中', 5000));
            Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8));
        }
    }

    [TestMethod]
    public void TestTruncatedUtf8Tail()
    {
        var utf8 = Encoding.UTF8.GetBytes("日志中文");
        Assert.AreEqual(Encoding.UTF8, EncodingDetector.DetectEncoding(utf8[..^1]));
    }

    [TestMethod]
    public async Task TestReadStreamWithTruncatedUtf8Tail()
    {
        var utf8 = Encoding.UTF8.GetBytes("abc中文");
        var text = await Files.ReadAllTextOrEmptyAsync(new MemoryStream(utf8[..^1]));
        Assert.AreEqual("abc中\uFFFD", text);
    }
}