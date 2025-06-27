using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Helper;

namespace PCL.Test;

[TestClass]
public class ByteHelperTest
{
    [TestMethod]
    public void TestTransform()
    {
        var random = new Random(DateTime.Now.Millisecond);
        var wathcer = new Stopwatch();
        const int maxTestCount = 1000;
        wathcer.Start();
        for (int i = 0; i < maxTestCount; i++)
        {
            ByteHelper.GetReadableLength(random.Next(1024, 10240000));
        }
        wathcer.Stop();
        Console.WriteLine($"Took {wathcer.ElapsedMilliseconds} ms to format {maxTestCount} data size.");
        Console.WriteLine($"Avg: {(double)wathcer.ElapsedMilliseconds/maxTestCount} ms/per");
    }
}