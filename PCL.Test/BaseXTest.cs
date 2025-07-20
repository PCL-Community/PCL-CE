using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Extension;

namespace PCL.Test;

[TestClass]
public class BaseXTest
{
    [TestMethod]
    public void TestBase36()
    {
        const string input = "999999999999999";
        Console.WriteLine(input);
        var o1 = input.FromB10ToB36();
        Console.WriteLine(o1);
        var o2 = o1.FromB36ToB10();
        Console.WriteLine(o2);
        Assert.AreEqual(input, o2);
    }
    
    [TestMethod]
    public void TestBase32()
    {
        const string input = "999999999999999";
        Console.WriteLine(input);
        var o1 = input.FromB10ToB32();
        Console.WriteLine(o1);
        var o2 = o1.FromB32ToB10();
        Console.WriteLine(o2);
        Assert.AreEqual(input, o2);
    }
}
