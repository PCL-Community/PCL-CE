using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Helper.Diff;

namespace PCL.Test;

[TestClass]
public class DiffTest
{
    [TestMethod]
    public async Task TestBsDiff()
    {
        var diff = new BsDiff();
        var res = await diff.Apply(
            [
                73, 32, 97, 109, 32, 110, 111, 116, 32, 115, 117, 114, 101, 32, 104, 111, 119, 32, 99, 104, 111, 117,
                108,
                100, 32, 98, 115, 100, 105, 102, 102, 32, 119, 111, 114, 107
            ],
            [
                66, 83, 68, 73, 70, 70, 52, 48, 54, 0, 0, 0, 0, 0, 0, 0, 39, 0, 0, 0, 0, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0,
                0, 66, 90, 104, 57, 49, 65, 89, 38, 83, 89, 247, 165, 175, 102, 0, 0, 17, 192, 64, 94, 172, 64, 0, 32,
                0, 33, 41, 164, 201, 232, 16, 3, 7, 87, 127, 120, 200, 130, 144, 45, 201, 94, 39, 120, 93, 201, 20, 225,
                66, 67, 222, 150, 189, 152, 66, 90, 104, 57, 49, 65, 89, 38, 83, 89, 76, 125, 55, 245, 0, 0, 0, 96, 0,
                64, 0, 1, 0, 32, 0, 33, 0, 130, 131, 23, 114, 69, 56, 80, 144, 76, 125, 55, 245, 66, 90, 104, 57, 49,
                65, 89, 38, 83, 89, 108, 69, 160, 122, 0, 0, 1, 1, 128, 2, 0, 17, 32, 32, 0, 33, 154, 104, 51, 77, 48,
                188, 93, 201, 20, 225, 66, 65, 177, 22, 129, 232
            ]);
        Console.WriteLine(string.Join(",",res));
        byte[] trueData =
        [
            73, 32, 97, 109, 32, 118, 101, 114, 121, 32, 115, 117, 114, 101, 32, 104, 111, 119, 32, 98, 115, 100, 105,
            102, 102, 32, 119, 111, 114, 107
        ];
        Assert.IsTrue(res.Length == trueData.Length);
        for (int i = 0; i < res.Length; i++)
            Assert.IsTrue(res[i] == trueData[i]);
    }

    [TestMethod]
    public async Task TestBsDiff2()
    {
        const string from = @"";
        const string diffFile = @"";
        const string outFile = @"";
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(outFile) || string.IsNullOrEmpty(diffFile))
            return;
        var diff = new BsDiff();
        File.WriteAllBytes(outFile, await diff.Apply(File.ReadAllBytes(from), File.ReadAllBytes(diffFile)));
    }
}