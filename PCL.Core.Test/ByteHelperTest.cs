using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test;

[TestClass]
public class ByteHelperTest
{
    [TestMethod]
    public void TestTransform()
    {
        // 断言具体格式化结果。此前仅校验「非空字符串」，
        // 于是 GetReadableLength 对所有输入返回同一段错误文本也照样通过。
        Assert.AreEqual("0 B", ByteStream.GetReadableLength(0), "0 字节格式化不符");
        Assert.AreEqual("512 B", ByteStream.GetReadableLength(512), "字节级格式化不符");
        Assert.AreEqual("1023 B", ByteStream.GetReadableLength(1023), "单位进位边界不符");
        Assert.AreEqual("1 KiB", ByteStream.GetReadableLength(1024), "KiB 进位不符");
        Assert.AreEqual("1.5 KiB", ByteStream.GetReadableLength(1536), "KiB 小数保留不符");
        Assert.AreEqual("1 MiB", ByteStream.GetReadableLength(1024 * 1024), "MiB 进位不符");
        Assert.AreEqual("1.5 MiB", ByteStream.GetReadableLength(1024 * 1024 * 3 / 2), "MiB 小数保留不符");
        Assert.AreEqual("1 GiB", ByteStream.GetReadableLength(1024L * 1024 * 1024), "GiB 进位不符");
        Assert.AreEqual("-2 KiB", ByteStream.GetReadableLength(-2048), "负值格式化不符");
    }

    [TestMethod]
    public void TestTransformInRange()
    {
        // 覆盖量级分布：单位必须随数值单调递增，且每档都能解析出正数
        string[] expectedUnits = ["KiB", "MiB", "GiB"];
        var previousUnitIndex = -1;

        foreach (var length in new[]
                 {
                     1024L, 10 * 1024L, 1024L * 1024, 10 * 1024L * 1024, 1024L * 1024 * 1024, 5L * 1024 * 1024 * 1024
                 })
        {
            var text = ByteStream.GetReadableLength(length);
            var parts = text.Split(' ');
            Assert.AreEqual(2, parts.Length, $"格式化 {length} 字节返回了意外格式：{text}");

            var unitIndex = Array.IndexOf(expectedUnits, parts[1]);
            Assert.IsTrue(unitIndex >= 0, $"格式化 {length} 字节返回了未知单位：{text}");
            Assert.IsTrue(unitIndex >= previousUnitIndex, $"格式化 {length} 字节的单位未随量级递增：{text}");
            previousUnitIndex = unitIndex;

            Assert.IsTrue(double.TryParse(parts[0], out var value) && value > 0,
                $"格式化 {length} 字节的数值部分无效：{text}");
        }
    }

    [TestMethod]
    public void TestStreamReadableLength()
    {
        // 实例方法必须反映流的真实长度，而不是固定返回
        using var stream = new MemoryStream(new byte[2048]);
        var byteStream = new ByteStream(stream);
        Assert.AreEqual("2 KiB", byteStream.GetReadableLength(), "流长度格式化不符");
    }
}
