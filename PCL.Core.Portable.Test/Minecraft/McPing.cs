// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PCL.Core.Link.McPing;
using PCL.Core.Utils;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class McPingTest
{
    [TestMethod]
    public async Task ModernPingParsesLocalStatusServer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var server = ServeModernAsync(listener);

        using var service = McPingServiceFactory.CreateService(endpoint, timeout: 2_000);
        var result = await service.PingAsync(TestContext.CancellationToken);
        await server;

        Assert.IsNotNull(result);
        Assert.AreEqual("1.21.5", result.Version.Name);
        Assert.AreEqual(2, result.Players.Online);
        Assert.AreEqual(20, result.Players.Max);
        Assert.AreEqual("§aLocal §lServer", result.Description);
        Assert.IsTrue(result.PreventsChatReports);
    }

    [TestMethod]
    public async Task LegacyPingParsesUtf16Response()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var server = ServeLegacyAsync(listener);

        using var service = McPingServiceFactory.CreateLegacyService(endpoint, timeout: 2_000);
        var result = await service.PingAsync(TestContext.CancellationToken);
        await server;

        Assert.IsNotNull(result);
        Assert.AreEqual("1.6.4", result.Version.Name);
        Assert.AreEqual("Legacy server", result.Description);
        Assert.AreEqual(3, result.Players.Online);
        Assert.AreEqual(12, result.Players.Max);
    }

    public TestContext TestContext { get; set; } = null!;

    private static async Task ServeModernAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadPacketAsync(stream);
        _ = await ReadPacketAsync(stream);

        const string json =
            """{"version":{"name":"1.21.5","protocol":770},"players":{"max":20,"online":2,"sample":[]},"description":{"color":"green","text":"Local ","extra":[{"bold":true,"text":"Server"}]},"preventsChatReports":true}""";
        await stream.WriteAsync(BuildStatusPacket(json));

        var ping = await ReadPacketAsync(stream);
        Assert.AreEqual(1UL, VarIntHelper.Decode(ping, out var offset));
        var timestamp = BinaryPrimitives.ReadInt64BigEndian(ping.AsSpan(offset));
        await stream.WriteAsync(BuildPongPacket(timestamp));
    }

    private static async Task ServeLegacyAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var query = new byte[2];
        await stream.ReadExactlyAsync(query);
        CollectionAssert.AreEqual(new byte[] { 0xFE, 0x01 }, query);

        var payload = string.Join('\0', ["§1", "78", "1.6.4", "Legacy server", "3", "12"]);
        var encoded = Encoding.BigEndianUnicode.GetBytes(payload);
        var response = new byte[3 + encoded.Length];
        response[0] = 0xFF;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(1, 2), checked((ushort)payload.Length));
        encoded.CopyTo(response.AsSpan(3));
        await stream.WriteAsync(response);
    }

    private static async Task<byte[]> ReadPacketAsync(Stream stream)
    {
        var length = checked((int)await VarIntHelper.ReadFromStreamAsync(stream));
        var packet = new byte[length];
        await stream.ReadExactlyAsync(packet);
        return packet;
    }

    private static byte[] BuildStatusPacket(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonLength = VarIntHelper.Encode((uint)jsonBytes.Length);
        var payload = new byte[1 + jsonLength.Length + jsonBytes.Length];
        jsonLength.CopyTo(payload, 1);
        jsonBytes.CopyTo(payload, 1 + jsonLength.Length);
        return Frame(payload);
    }

    private static byte[] BuildPongPacket(long timestamp)
    {
        var payload = new byte[9];
        payload[0] = 1;
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(1), timestamp);
        return Frame(payload);
    }

    private static byte[] Frame(byte[] payload)
    {
        var length = VarIntHelper.Encode((uint)payload.Length);
        var packet = new byte[length.Length + payload.Length];
        length.CopyTo(packet, 0);
        payload.CopyTo(packet, length.Length);
        return packet;
    }
}
