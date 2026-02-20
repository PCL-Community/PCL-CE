using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Dns;

namespace PCL.Core.Test.Network;

[TestClass]
public class DoHQueryTest
{
    [TestMethod]
    public async Task TestIpQuery()
    {
        var query = DnsQuery.Instance;
        var addr = await query.QueryForIpAsync("cloudflare.com", TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(addr);
        Assert.IsGreaterThan(0, addr.Length);
        Console.WriteLine(string.Join(", ", addr.Select(x => x.ToString())));
    }

    [TestMethod]
    public async Task TestSrvQuery()
    {
        var query = DnsQuery.Instance;
        var addr = await query.QueryAsync("_minecraft._tcp.mc.hdeda6e85.nyat.app", DnsQueryType.SRV, TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(addr);
        Assert.IsGreaterThan(0, addr.Answers.Count);
        Assert.AreEqual(DnsQueryClass.IN, addr.Header.QueryClass);
        var record = addr.Answers.FirstOrDefault()?.Resource as DnsUnknownResource;
        Assert.IsNotNull(record);
        var srvRecord = new DnsSrvResource();
        var offset = 0;
        srvRecord.ReadBytes(record.Raw, ref offset, record.Raw.Length);
        Console.WriteLine(srvRecord.Target);
        Console.WriteLine(srvRecord.Port);
    }

    public TestContext TestContext { get; set; }
}