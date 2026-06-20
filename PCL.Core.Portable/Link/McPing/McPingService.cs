// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Link.McPing;

/// <summary>
/// Minecraft 1.7+ status protocol client.
/// </summary>
public class McPingService : IMcPingService
{
    private const int DefaultTimeout = 10_000;
    private const int MaxPacketLength = 2 * 1024 * 1024;
    private const int ProtocolVersion = 772;
    private const string ModuleName = "McPing";
    private static readonly byte[] _StatusRequestPacket = [1, 0];

    private readonly IPEndPoint _endpoint;
    private readonly string _host;
    private readonly int _timeout;
    private bool _disposed;

    public IPEndPoint Endpoint => _endpoint;
    public string Host => _host;
    public int Timeout => _timeout;

    public McPingService(IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, 0);
        _endpoint = endpoint;
        _host = endpoint.Address.ToString();
        _timeout = timeout;
    }

    public McPingService(string host, int port = 25565, int timeout = DefaultTimeout)
        : this(host, ResolveEndpoint(host, port), timeout)
    {
    }

    public McPingService(string host, IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, 0);
        _host = host;
        _endpoint = endpoint;
        _timeout = timeout;
    }

    public async Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            await socket.ConnectAsync(_endpoint, linkedCts.Token).ConfigureAwait(false);
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            await stream.WriteAsync(BuildHandshakePacket(_host, _endpoint.Port), linkedCts.Token)
                .ConfigureAwait(false);
            await stream.WriteAsync(_StatusRequestPacket, linkedCts.Token).ConfigureAwait(false);

            var statusPacket = await ReadPacketAsync(stream, linkedCts.Token).ConfigureAwait(false);
            var result = ParseStatusPacket(statusPacket);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await stream.WriteAsync(BuildPingPacket(timestamp), linkedCts.Token).ConfigureAwait(false);
            var pongPacket = await ReadPacketAsync(stream, linkedCts.Token).ConfigureAwait(false);
            var echoedTimestamp = ParsePongPacket(pongPacket);
            var latency = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - echoedTimestamp);

            return result with { Latency = latency };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            PortableLog.Warn(ex, ModuleName, $"Server query timed out: {_endpoint}");
            return null;
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException)
        {
            PortableLog.Warn(ex, ModuleName, $"Server query failed: {_endpoint}");
            return null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static IPEndPoint ResolveEndpoint(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if ((uint)port > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (IPAddress.TryParse(host, out var parsed))
            return new IPEndPoint(parsed, port);

        var addresses = Dns.GetHostAddresses(host);
        var address = Array.Find(addresses, candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                      ?? addresses.FirstOrDefault()
                      ?? throw new SocketException((int)SocketError.HostNotFound);
        return new IPEndPoint(address, port);
    }

    private static byte[] BuildHandshakePacket(string host, int port)
    {
        var hostByteCount = Encoding.UTF8.GetByteCount(host);
        if (hostByteCount > ushort.MaxValue)
            throw new ArgumentException("The server address is too long.", nameof(host));

        var payloadLength =
            GetVarIntLength(0) +
            GetVarIntLength(ProtocolVersion) +
            GetVarIntLength((ulong)hostByteCount) +
            hostByteCount +
            sizeof(ushort) +
            GetVarIntLength(1);
        var packetLength = GetVarIntLength((ulong)payloadLength) + payloadLength;
        var packet = GC.AllocateUninitializedArray<byte>(packetLength);
        var destination = packet.AsSpan();
        var offset = 0;

        WriteVarInt((ulong)payloadLength, destination, ref offset);
        WriteVarInt(0, destination, ref offset);
        WriteVarInt(ProtocolVersion, destination, ref offset);
        WriteVarInt((ulong)hostByteCount, destination, ref offset);
        offset += Encoding.UTF8.GetBytes(host, destination[offset..]);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(offset, sizeof(ushort)), checked((ushort)port));
        offset += sizeof(ushort);
        WriteVarInt(1, destination, ref offset);
        return packet;
    }

    private static byte[] BuildPingPacket(long timestamp)
    {
        var packet = GC.AllocateUninitializedArray<byte>(10);
        packet[0] = 9;
        packet[1] = 1;
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(2), timestamp);
        return packet;
    }

    private static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken cancellationToken)
    {
        var length = checked((int)await VarIntHelper.ReadFromStreamAsync(stream, cancellationToken)
            .ConfigureAwait(false));
        if (length <= 0 || length > MaxPacketLength)
            throw new InvalidDataException($"Invalid Minecraft packet length: {length}.");

        var packet = GC.AllocateUninitializedArray<byte>(length);
        await stream.ReadExactlyAsync(packet, cancellationToken).ConfigureAwait(false);
        return packet;
    }

    private static McPingResult ParseStatusPacket(ReadOnlySpan<byte> packet)
    {
        var offset = 0;
        if (ReadVarInt(packet, ref offset) != 0)
            throw new InvalidDataException("Expected a Minecraft status response packet.");

        var jsonLength = checked((int)ReadVarInt(packet, ref offset));
        if (jsonLength < 0 || jsonLength > packet.Length - offset)
            throw new InvalidDataException("The Minecraft status response contains an invalid JSON length.");

        var root = JsonNode.Parse(packet.Slice(offset, jsonLength)) as JsonObject
                   ?? throw new InvalidDataException("The Minecraft status response is not a JSON object.");
        return ParseStatus(root);
    }

    private static long ParsePongPacket(ReadOnlySpan<byte> packet)
    {
        var offset = 0;
        if (ReadVarInt(packet, ref offset) != 1 || packet.Length - offset != sizeof(long))
            throw new InvalidDataException("The Minecraft pong packet is invalid.");
        return BinaryPrimitives.ReadInt64BigEndian(packet[offset..]);
    }

    private static McPingResult ParseStatus(JsonObject root)
    {
        var version = root["version"] as JsonObject;
        var players = root["players"] as JsonObject;
        var samples = new List<McPingPlayerSampleResult>();
        if (players?["sample"] is JsonArray sampleArray)
        {
            foreach (var item in sampleArray.OfType<JsonObject>())
                samples.Add(new McPingPlayerSampleResult(GetString(item["name"]), GetString(item["id"])));
        }

        McPingModInfoResult? modInfo = null;
        if (root["modinfo"] is JsonObject modInfoNode)
        {
            var mods = new List<McPingModInfoModResult>();
            if ((modInfoNode["modList"] ?? modInfoNode["modlist"]) is JsonArray modArray)
            {
                foreach (var item in modArray.OfType<JsonObject>())
                    mods.Add(new McPingModInfoModResult(GetString(item["modid"]), GetString(item["version"])));
            }
            modInfo = new McPingModInfoResult(GetString(modInfoNode["type"]), mods);
        }

        return new McPingResult(
            new McPingVersionResult(GetString(version?["name"]), GetInt32(version?["protocol"])),
            new McPingPlayerResult(GetInt32(players?["max"]), GetInt32(players?["online"]), samples),
            FlattenDescription(root["description"]),
            GetNullableString(root["favicon"]),
            0,
            modInfo,
            GetNullableBoolean(root["preventsChatReports"]));
    }

    private static string FlattenDescription(JsonNode? node)
    {
        if (node is null)
            return string.Empty;
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        var builder = new StringBuilder();
        AppendDescription(node, builder);
        return builder.ToString();
    }

    private static void AppendDescription(JsonNode node, StringBuilder builder)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                builder.Append(text);
                return;
            case JsonArray array:
                foreach (var child in array)
                    if (child is not null)
                        AppendDescription(child, builder);
                return;
            case JsonObject obj:
                builder.Append(GetTextStyle(
                    GetString(obj["color"]),
                    GetBoolean(obj["bold"]),
                    GetBoolean(obj["obfuscated"]),
                    GetBoolean(obj["strikethrough"]),
                    GetBoolean(obj["underlined"] ?? obj["underline"]),
                    GetBoolean(obj["italic"])));
                if (obj["text"] is { } textNode)
                    AppendDescription(textNode, builder);
                if (obj["extra"] is JsonArray extra)
                    foreach (var child in extra)
                        if (child is not null)
                            AppendDescription(child, builder);
                return;
        }
    }

    private static int GetVarIntLength(ulong value)
    {
        var length = 1;
        while ((value >>= 7) != 0)
            length++;
        return length;
    }

    private static void WriteVarInt(ulong value, Span<byte> destination, ref int offset)
    {
        if (!VarIntHelper.TryEncode(value, destination[offset..], out var written))
            throw new InvalidOperationException("The packet buffer is too small.");
        offset += written;
    }

    private static ulong ReadVarInt(ReadOnlySpan<byte> source, ref int offset)
    {
        var value = VarIntHelper.Decode(source[offset..], out var read);
        offset += read;
        return value;
    }

    private static string GetString(JsonNode? node) => GetNullableString(node) ?? string.Empty;

    private static string? GetNullableString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;

    private static int GetInt32(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var result))
            return result;
        return int.TryParse(node?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
            ? result
            : 0;
    }

    private static bool GetBoolean(JsonNode? node) => GetNullableBoolean(node) == true;

    private static bool? GetNullableBoolean(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var result))
            return result;
        return bool.TryParse(node?.ToString(), out result) ? result : null;
    }

    private static readonly FrozenDictionary<string, string> _ColorMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["black"] = "0",
            ["dark_blue"] = "1",
            ["dark_green"] = "2",
            ["dark_aqua"] = "3",
            ["dark_red"] = "4",
            ["dark_purple"] = "5",
            ["gold"] = "6",
            ["gray"] = "7",
            ["dark_gray"] = "8",
            ["blue"] = "9",
            ["green"] = "a",
            ["aqua"] = "b",
            ["red"] = "c",
            ["light_purple"] = "d",
            ["yellow"] = "e",
            ["white"] = "f"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static string GetTextStyle(
        string color,
        bool bold,
        bool obfuscated,
        bool strikethrough,
        bool underline,
        bool italic)
    {
        var builder = new StringBuilder(12);
        if (_ColorMap.TryGetValue(color, out var colorCode))
            builder.Append('§').Append(colorCode);
        else if (color.StartsWith('#'))
            builder.Append(color);
        if (bold)
            builder.Append("§l");
        if (italic)
            builder.Append("§o");
        if (obfuscated)
            builder.Append("§k");
        if (underline)
            builder.Append("§n");
        if (strikethrough)
            builder.Append("§m");
        return builder.ToString();
    }
}
