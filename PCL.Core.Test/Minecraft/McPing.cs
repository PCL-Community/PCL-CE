using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using PCL.Core.Link.McPing;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class McPingTest
{
    [TestMethod]
    public async Task PingTest()
    {
        using var ping2 = McPingServiceFactory.CreateService("mc.hypixel.net", 25565);
        var res = await ping2.PingAsync();
        Assert.IsNotNull(res);
        Console.WriteLine(res.Description);

        using var ping1 = McPingServiceFactory.CreateService("mc233.cn", 25565);
        res = await ping1.PingAsync();
        Assert.IsNotNull(res);
        Console.WriteLine(res.Description);
    }

    [TestMethod]
    public async Task PingTestOld()
    {
        using var ping = McPingServiceFactory.CreateLegacyService("127.0.0.1", 58383);
        var res = await ping.PingAsync();
        Assert.IsNotNull(res);
        Console.WriteLine(res.Description);
    }
}