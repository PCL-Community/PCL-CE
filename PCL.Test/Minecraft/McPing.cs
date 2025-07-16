using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Minecraft;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace PCL.Test.Minecraft;

[TestClass]
public class McPingTest
{
    [TestMethod]
    public async Task PingTest()
    {
        using var so = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var ping2 = new McPing("mc.hypixel.net", 25565);
        var res = await ping2.PingAsync();
        Console.WriteLine(res.Description);

        using var ping1 = new McPing("mc233.cn", 25565);
        res = await ping1.PingAsync();
        Console.WriteLine(res.Description);
    }
}