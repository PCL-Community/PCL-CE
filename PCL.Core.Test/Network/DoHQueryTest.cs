using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Dns;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Test.Network;

[TestClass]
public class DoHQueryTest
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task TestIpQuery()
    {
        var query = DnsQuery.Instance;
        var addr = await QueryOrInconclusiveAsync(
            () => query.QueryForIpAsync("cloudflare.com", TestContext.CancellationTokenSource.Token));
        Assert.IsNotNull(addr);
        if (addr.Length == 0)
            Assert.Inconclusive("The public DNS query returned no addresses.");
        Console.WriteLine(string.Join(", ", addr.Select(x => x.ToString())));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task TestSrvQuery()
    {
        var query = DnsQuery.Instance;
        var addr = await QueryOrInconclusiveAsync(
            () => query.QueryAsync(
                "_minecraft._tcp.mc.hdeda6e85.nyat.app",
                DnsQueryType.SRV,
                TestContext.CancellationTokenSource.Token));
        Assert.IsNotNull(addr);
        if (addr.Answers.Count == 0)
            Assert.Inconclusive("The public SRV record is currently unavailable.");
        Assert.AreEqual(DnsQueryClass.IN, addr.Header.QueryClass);
        var record = addr.Answers.FirstOrDefault()?.Resource as DnsUnknownResource;
        Assert.IsNotNull(record);
        var srvRecord = new DnsSrvResource();
        var offset = 0;
        srvRecord.ReadBytes(record.Raw, ref offset, record.Raw.Length);
        Console.WriteLine(srvRecord.Target);
        Console.WriteLine(srvRecord.Port);
    }

    private static async Task<T> QueryOrInconclusiveAsync<T>(Func<Task<T>> query)
    {
        try
        {
            return await query();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Assert.Inconclusive($"DoH endpoint is unavailable: {ex.Message}");
            throw new InvalidOperationException("Unreachable");
        }
    }

    public TestContext TestContext { get; set; }
}
