using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Net.Sockets;
using PCL.Core.Link.McPing;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class McPingTest
{
    [TestMethod]
    public async Task PingTest()
    {
        using var so = new Socket(SocketType.Stream, ProtocolType.Tcp);
        using var ping2 = McPingServiceFactory.CreateService("mc.hypixel.net", 25565);
        var res = await ping2.PingAsync(TestContext.CancellationToken).ConfigureAwait(false);
        Assert.IsNotNull(res);
        Console.WriteLine(res.Description);

        using var ping1 = McPingServiceFactory.CreateService("mc233.cn", 25565);
        res = await ping1.PingAsync(TestContext.CancellationToken).ConfigureAwait(false);
        Assert.IsNotNull(res);
        Console.WriteLine(res.Description);
    }

    public TestContext TestContext { get; set; }
}