using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Hash;
using PCL.Core.Utils.Exts;
using System.Text;

namespace PCL.Core.Test.Hash;

[TestClass]
public class MurmurHash2Test
{
    [TestMethod]
    public void Test()
    {
        const string input = "aklerfdhvkore;fhjbgoiwrfgbuio34htgb889rguiyufgvueirefvrvu9vhgg9wygf94u8fgw249fyhuwygf293ghf8h";
        const uint output = 2672531333;
        var buf = Encoding.UTF8.GetBytes(input);
        var result = MurmurHash2Provider.Instance.ComputeHash(buf);
        Assert.AreEqual(output, BitConverter.ToUInt32(result));

        var hex = result.ToHexString().HexToBytes();
        Assert.AreEqual(output, BitConverter.ToUInt32(hex));
    }
}
