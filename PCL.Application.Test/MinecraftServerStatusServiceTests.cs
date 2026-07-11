// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Instances;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftServerStatusServiceTests
{
    [TestMethod]
    public async Task QueryAsync_UsesStatusProtocolAndParsesResponse()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        Task server = ServeStatusAsync(listener, timeout.Token);

        MinecraftServerStatusService service = new();
        MinecraftServerStatus status = await service.QueryAsync($"127.0.0.1:{port}", timeout.Token);
        await server;

        Assert.AreEqual("Hello world", status.Description);
        Assert.AreEqual(3, status.OnlinePlayers);
        Assert.AreEqual(20, status.MaximumPlayers);
        Assert.AreEqual("1.21.5", status.VersionName);
        Assert.AreEqual(770, status.ProtocolVersion);
        Assert.AreEqual("data:image/png;base64,AA==", status.Icon);
        Assert.IsTrue(status.Latency >= TimeSpan.Zero);
    }

    [TestMethod]
    public async Task QueryAsync_RejectsInvalidPort()
    {
        MinecraftServerStatusService service = new();
        await Assert.ThrowsExactlyAsync<FormatException>(() => service.QueryAsync("localhost:not-a-port"));
    }

    private static async Task ServeStatusAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        _ = await ReadPacketAsync(stream, cancellationToken);
        byte[] request = await ReadPacketAsync(stream, cancellationToken);
        Assert.AreEqual(0, request.Single());

        const string json = """
            {
              "version":{"name":"1.21.5","protocol":770},
              "players":{"max":20,"online":3},
              "description":{"text":"§aHello ","extra":[{"text":"world"}]},
              "favicon":"data:image/png;base64,AA=="
            }
            """;
        using MemoryStream statusPacket = new();
        WriteVarInt(statusPacket, 0);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        WriteVarInt(statusPacket, jsonBytes.Length);
        statusPacket.Write(jsonBytes);
        await WritePacketAsync(stream, statusPacket.ToArray(), cancellationToken);

        byte[] ping = await ReadPacketAsync(stream, cancellationToken);
        Assert.AreEqual(9, ping.Length);
        Assert.AreEqual(1, ping[0]);
        await WritePacketAsync(stream, ping, cancellationToken);
    }

    private static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        int length = await ReadVarIntAsync(stream, cancellationToken);
        byte[] packet = new byte[length];
        await stream.ReadExactlyAsync(packet, cancellationToken);
        return packet;
    }

    private static async Task WritePacketAsync(
        Stream stream,
        byte[] packet,
        CancellationToken cancellationToken)
    {
        using MemoryStream frame = new();
        WriteVarInt(frame, packet.Length);
        frame.Write(packet);
        await stream.WriteAsync(frame.ToArray(), cancellationToken);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        uint remaining = unchecked((uint)value);
        do
        {
            byte current = (byte)(remaining & 0x7f);
            remaining >>= 7;
            if (remaining != 0)
                current |= 0x80;
            stream.WriteByte(current);
        }
        while (remaining != 0);
    }

    private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        int result = 0;
        byte[] value = new byte[1];
        for (int position = 0; position < 5; position++)
        {
            await stream.ReadExactlyAsync(value, cancellationToken);
            result |= (value[0] & 0x7f) << (position * 7);
            if ((value[0] & 0x80) == 0)
                return result;
        }
        throw new InvalidDataException("VarInt too long.");
    }
}
