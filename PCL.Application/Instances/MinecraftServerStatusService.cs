// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PCL.Application.Instances;

public sealed record MinecraftServerStatus(
    string Description,
    int OnlinePlayers,
    int MaximumPlayers,
    string VersionName,
    int ProtocolVersion,
    TimeSpan Latency,
    string? Icon);

public interface IMinecraftServerStatusService
{
    Task<MinecraftServerStatus> QueryAsync(
        string address,
        CancellationToken cancellationToken = default);
}

public sealed class MinecraftServerStatusService : IMinecraftServerStatusService
{
    private const int DefaultPort = 25565;
    private const int MaximumPacketLength = 1024 * 1024;

    public async Task<MinecraftServerStatus> QueryAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        (string host, int port) = ParseAddress(address);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        using TcpClient client = new();
        await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
        await using NetworkStream stream = client.GetStream();

        await WriteHandshakeAsync(stream, host, port, timeout.Token).ConfigureAwait(false);
        await stream.WriteAsync(new byte[] { 1, 0 }, timeout.Token).ConfigureAwait(false);

        string json = await ReadStatusJsonAsync(stream, timeout.Token).ConfigureAwait(false);
        Stopwatch latencyTimer = Stopwatch.StartNew();
        await WritePingAsync(stream, timeout.Token).ConfigureAwait(false);
        await ReadPingResponseAsync(stream, timeout.Token).ConfigureAwait(false);
        latencyTimer.Stop();

        return ParseStatus(json, latencyTimer.Elapsed);
    }

    private static (string Host, int Port) ParseAddress(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        string trimmed = address.Trim();
        if (trimmed.StartsWith('['))
        {
            int closingBracket = trimmed.IndexOf(']');
            if (closingBracket <= 1)
                throw new FormatException("服务器 IPv6 地址格式无效。");

            string host = trimmed[1..closingBracket];
            if (closingBracket == trimmed.Length - 1)
                return (host, DefaultPort);
            if (trimmed[closingBracket + 1] != ':' ||
                !TryParsePort(trimmed[(closingBracket + 2)..], out int port))
            {
                throw new FormatException("服务器端口格式无效。");
            }
            return (host, port);
        }

        int firstColon = trimmed.IndexOf(':');
        int lastColon = trimmed.LastIndexOf(':');
        if (firstColon >= 0 && firstColon == lastColon)
        {
            if (!TryParsePort(trimmed[(lastColon + 1)..], out int port))
                throw new FormatException("服务器端口格式无效。");
            string host = trimmed[..lastColon];
            if (string.IsNullOrWhiteSpace(host))
                throw new FormatException("服务器地址不能为空。");
            return (host, port);
        }

        return (trimmed, DefaultPort);
    }

    private static bool TryParsePort(string value, out int port) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port) &&
        port is > 0 and <= ushort.MaxValue;

    private static async Task WriteHandshakeAsync(
        Stream stream,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        using MemoryStream packet = new();
        WriteVarInt(packet, 0);
        WriteVarInt(packet, -1);
        WriteString(packet, host);
        Span<byte> portBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(portBytes, (ushort)port);
        packet.Write(portBytes);
        WriteVarInt(packet, 1);
        await WritePacketAsync(stream, packet.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadStatusJsonAsync(Stream stream, CancellationToken cancellationToken)
    {
        int packetLength = await ReadVarIntAsync(stream, cancellationToken).ConfigureAwait(false);
        ValidatePacketLength(packetLength);
        byte[] packet = new byte[packetLength];
        await ReadExactlyAsync(stream, packet, cancellationToken).ConfigureAwait(false);

        using MemoryStream content = new(packet, writable: false);
        int packetId = ReadVarInt(content);
        if (packetId != 0)
            throw new InvalidDataException("服务器返回了无效的状态响应。");

        int stringLength = ReadVarInt(content);
        if (stringLength < 0 || stringLength > MaximumPacketLength || stringLength > content.Length - content.Position)
            throw new InvalidDataException("服务器状态文本长度无效。");
        byte[] json = new byte[stringLength];
        content.ReadExactly(json);
        return Encoding.UTF8.GetString(json);
    }

    private static async Task WritePingAsync(Stream stream, CancellationToken cancellationToken)
    {
        using MemoryStream packet = new();
        WriteVarInt(packet, 1);
        Span<byte> payload = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(payload, Stopwatch.GetTimestamp());
        packet.Write(payload);
        await WritePacketAsync(stream, packet.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadPingResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        int packetLength = await ReadVarIntAsync(stream, cancellationToken).ConfigureAwait(false);
        if (packetLength != 9)
            throw new InvalidDataException("服务器返回了无效的延迟响应。");
        byte[] packet = new byte[packetLength];
        await ReadExactlyAsync(stream, packet, cancellationToken).ConfigureAwait(false);
        using MemoryStream content = new(packet, writable: false);
        if (ReadVarInt(content) != 1)
            throw new InvalidDataException("服务器返回了无效的延迟响应。");
    }

    private static async Task WritePacketAsync(
        Stream stream,
        byte[] packet,
        CancellationToken cancellationToken)
    {
        using MemoryStream framed = new();
        WriteVarInt(framed, packet.Length);
        framed.Write(packet);
        await stream.WriteAsync(framed.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static MinecraftServerStatus ParseStatus(string json, TimeSpan latency)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string description = root.TryGetProperty("description", out JsonElement descriptionElement)
            ? ReadDescription(descriptionElement)
            : string.Empty;
        int online = ReadInteger(root, "players", "online");
        int maximum = ReadInteger(root, "players", "max");
        string versionName = ReadString(root, "version", "name");
        int protocol = ReadInteger(root, "version", "protocol");
        string? icon = root.TryGetProperty("favicon", out JsonElement favicon) && favicon.ValueKind == JsonValueKind.String
            ? favicon.GetString()
            : null;
        return new MinecraftServerStatus(
            StripLegacyFormatting(description),
            online,
            maximum,
            versionName,
            protocol,
            latency,
            icon);
    }

    private static string ReadDescription(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? string.Empty;
        if (element.ValueKind != JsonValueKind.Object)
            return string.Empty;

        StringBuilder builder = new();
        if (element.TryGetProperty("text", out JsonElement text) && text.ValueKind == JsonValueKind.String)
            builder.Append(text.GetString());
        if (element.TryGetProperty("translate", out JsonElement translate) &&
            translate.ValueKind == JsonValueKind.String && builder.Length == 0)
        {
            builder.Append(translate.GetString());
        }
        if (element.TryGetProperty("extra", out JsonElement extra) && extra.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in extra.EnumerateArray())
                builder.Append(ReadDescription(child));
        }
        return builder.ToString();
    }

    private static int ReadInteger(JsonElement root, string objectName, string propertyName) =>
        root.TryGetProperty(objectName, out JsonElement parent) &&
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(propertyName, out JsonElement value) &&
        value.TryGetInt32(out int result)
            ? result
            : 0;

    private static string ReadString(JsonElement root, string objectName, string propertyName) =>
        root.TryGetProperty(objectName, out JsonElement parent) &&
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string StripLegacyFormatting(string value)
    {
        if (!value.Contains('§'))
            return value;

        StringBuilder builder = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == '§' && index + 1 < value.Length)
            {
                index++;
                continue;
            }
            builder.Append(value[index]);
        }
        return builder.ToString();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
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

    private static int ReadVarInt(Stream stream)
    {
        int value = 0;
        for (int position = 0; position < 5; position++)
        {
            int current = stream.ReadByte();
            if (current < 0)
                throw new EndOfStreamException();
            value |= (current & 0x7f) << (position * 7);
            if ((current & 0x80) == 0)
                return value;
        }
        throw new InvalidDataException("VarInt 长度超过协议限制。");
    }

    private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        int value = 0;
        byte[] buffer = new byte[1];
        for (int position = 0; position < 5; position++)
        {
            await ReadExactlyAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
            int current = buffer[0];
            value |= (current & 0x7f) << (position * 7);
            if ((current & 0x80) == 0)
                return value;
        }
        throw new InvalidDataException("VarInt 长度超过协议限制。");
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }

    private static void ValidatePacketLength(int packetLength)
    {
        if (packetLength is <= 0 or > MaximumPacketLength)
            throw new InvalidDataException("服务器响应包长度无效。");
    }
}
