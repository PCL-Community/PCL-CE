// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PCL.Desktop.Controls.Legacy;

public partial class MinecraftServer : Grid, IDisposable
{
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<MinecraftServer, string>(nameof(Address), string.Empty);

    private CancellationTokenSource? _addressCancellation;

    static MinecraftServer()
    {
        AddressProperty.Changed.AddClassHandler<MinecraftServer>(AddressChanged);
    }

    public MinecraftServer()
    {
        AvaloniaXamlLoader.Load(this);
        ResetServerInfo();
    }

    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    public void Dispose()
    {
        _addressCancellation?.Cancel();
        _addressCancellation?.Dispose();
        _addressCancellation = null;
        GC.SuppressFinalize(this);
    }

    public async Task UpdateServerInfoAsync(string? address, CancellationToken cancellationToken = default)
    {
        address = NormalizeAddress(address);
        if (string.IsNullOrWhiteSpace(address))
        {
            ShowClientMessage("请输入服务器地址。");
            return;
        }

        ResetServerInfo();
        SetText("LabServerDesc", "正在查询服务器...");
        SetText("MotdRenderer", address);
        SetText("LabServerPlayer", string.Empty);

        try
        {
            MinecraftServerStatus status = await QueryStatusAsync(address, cancellationToken).ConfigureAwait(true);
            ApplyStatus(status);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ShowClientMessage("无法获取服务器信息。", ex.Message);
        }
    }

    public void ShowClientMessage(string description, string? detail = null)
    {
        ResetServerInfo();
        SetText("LabServerDesc", description);
        SetText("MotdRenderer", detail ?? string.Empty);
        SetText("LabServerPlayer", string.Empty);
    }

    private static void AddressChanged(MinecraftServer control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string address || string.IsNullOrWhiteSpace(address))
            return;

        control._addressCancellation?.Cancel();
        control._addressCancellation?.Dispose();
        control._addressCancellation = new CancellationTokenSource();
        _ = control.UpdateServerInfoAsync(address, control._addressCancellation.Token);
    }

    private void ApplyStatus(MinecraftServerStatus status)
    {
        SetText("LabServerDesc", status.Description);
        SetText("MotdRenderer", status.Version);
        SetText(
            "LabServerPlayer",
            status.MaxPlayers > 0
                ? $"{status.OnlinePlayers}/{status.MaxPlayers} 人在线"
                : $"{status.OnlinePlayers} 人在线");

        if (!string.IsNullOrWhiteSpace(status.FaviconPngBase64))
            SetImage(status.FaviconPngBase64);
    }

    private void ResetServerInfo()
    {
        SetFallbackImage();
        SetText("LabServerDesc", "等待查询服务器。");
        SetText("MotdRenderer", string.Empty);
        SetText("LabServerPlayer", string.Empty);
    }

    private static string NormalizeAddress(string? address) =>
        (address ?? string.Empty).Trim().Replace('：', ':');

    private static async Task<MinecraftServerStatus> QueryStatusAsync(string address, CancellationToken cancellationToken)
    {
        ServerEndpoint endpoint = ServerEndpoint.Parse(address);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));

        using TcpClient client = new();
        await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token).ConfigureAwait(false);
        await using NetworkStream stream = client.GetStream();

        byte[] handshake = CreateHandshakePacket(endpoint);
        await stream.WriteAsync(handshake, timeout.Token).ConfigureAwait(false);
        byte[] statusRequestPacket = [1, 0];
        await stream.WriteAsync(statusRequestPacket, timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        _ = await ReadVarIntAsync(stream, timeout.Token).ConfigureAwait(false);
        int packetId = await ReadVarIntAsync(stream, timeout.Token).ConfigureAwait(false);
        if (packetId != 0)
            throw new InvalidDataException("服务器返回了无法识别的状态响应。");

        string json = await ReadStringAsync(stream, timeout.Token).ConfigureAwait(false);
        return ParseStatusJson(json);
    }

    private static byte[] CreateHandshakePacket(ServerEndpoint endpoint)
    {
        using MemoryStream payload = new();
        WriteVarInt(payload, 0);
        WriteVarInt(payload, 47);
        WriteString(payload, endpoint.Host);
        Span<byte> port = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(port, (ushort)endpoint.Port);
        payload.Write(port);
        WriteVarInt(payload, 1);

        byte[] payloadBytes = payload.ToArray();
        using MemoryStream packet = new();
        WriteVarInt(packet, payloadBytes.Length);
        packet.Write(payloadBytes);
        return packet.ToArray();
    }

    private static MinecraftServerStatus ParseStatusJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string description = root.TryGetProperty("description", out JsonElement descriptionElement)
            ? ReadMinecraftText(descriptionElement)
            : "服务器未返回介绍。";
        string version = root.TryGetProperty("version", out JsonElement versionElement) &&
            versionElement.TryGetProperty("name", out JsonElement versionName)
            ? versionName.GetString() ?? string.Empty
            : string.Empty;

        int online = 0;
        int max = 0;
        if (root.TryGetProperty("players", out JsonElement playersElement))
        {
            if (playersElement.TryGetProperty("online", out JsonElement onlineElement))
                online = onlineElement.GetInt32();
            if (playersElement.TryGetProperty("max", out JsonElement maxElement))
                max = maxElement.GetInt32();
        }

        string? favicon = null;
        if (root.TryGetProperty("favicon", out JsonElement faviconElement))
            favicon = NormalizeFavicon(faviconElement.GetString());

        return new MinecraftServerStatus(description, version, online, max, favicon);
    }

    private static string ReadMinecraftText(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;
            case JsonValueKind.Object:
                StringBuilder builder = new();
                if (element.TryGetProperty("text", out JsonElement text))
                    builder.Append(text.GetString());
                if (builder.Length == 0 && element.TryGetProperty("translate", out JsonElement translate))
                    builder.Append(translate.GetString());
                if (element.TryGetProperty("extra", out JsonElement extra) && extra.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement child in extra.EnumerateArray())
                        builder.Append(ReadMinecraftText(child));
                }

                return builder.ToString();
            case JsonValueKind.Array:
                StringBuilder arrayBuilder = new();
                foreach (JsonElement child in element.EnumerateArray())
                    arrayBuilder.Append(ReadMinecraftText(child));
                return arrayBuilder.ToString();
            default:
                return string.Empty;
        }
    }

    private static string? NormalizeFavicon(string? favicon)
    {
        const string prefix = "data:image/png;base64,";
        if (string.IsNullOrWhiteSpace(favicon))
            return null;

        return favicon.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? favicon[prefix.Length..]
            : favicon;
    }

    private static async Task<int> ReadVarIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        int value = 0;
        int position = 0;
        byte[] buffer = new byte[1];

        while (true)
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
            byte current = buffer[0];
            value |= (current & 0x7F) << position;

            if ((current & 0x80) == 0)
                return value;

            position += 7;
            if (position >= 35)
                throw new InvalidDataException("服务器返回了过长的 VarInt。");
        }
    }

    private static async Task<string> ReadStringAsync(Stream stream, CancellationToken cancellationToken)
    {
        int length = await ReadVarIntAsync(stream, cancellationToken).ConfigureAwait(false);
        if (length < 0 || length > 1_048_576)
            throw new InvalidDataException("服务器返回的数据过大。");

        byte[] buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer);
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        uint unsigned = unchecked((uint)value);
        while ((unsigned & ~0x7Fu) != 0)
        {
            stream.WriteByte((byte)((unsigned & 0x7F) | 0x80));
            unsigned >>= 7;
        }

        stream.WriteByte((byte)unsigned);
    }

    private void SetFallbackImage()
    {
        try
        {
            using Stream stream = AssetLoader.Open(
                new Uri("avares://PCL.Desktop/WpfOriginal/Images/Icons/DefaultServer.png"));
            SetImageSource(new Bitmap(stream));
        }
        catch (IOException)
        {
            SetImageSource(null);
        }
    }

    private void SetImage(string faviconPngBase64)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(faviconPngBase64);
            SetImageSource(new Bitmap(new MemoryStream(bytes)));
        }
        catch (FormatException)
        {
            SetFallbackImage();
        }
    }

    private void SetImageSource(Bitmap? source)
    {
        if (this.FindControl<Image>("ImgServerLogo") is { } image)
            image.Source = source;
    }

    private void SetText(string name, string text)
    {
        if (this.FindControl<TextBlock>(name) is { } block)
            block.Text = text;
    }

    private sealed record MinecraftServerStatus(
        string Description,
        string Version,
        int OnlinePlayers,
        int MaxPlayers,
        string? FaviconPngBase64);

    private readonly record struct ServerEndpoint(string Host, int Port)
    {
        public static ServerEndpoint Parse(string address)
        {
            string host = address;
            int port = 25565;
            int separator = address.LastIndexOf(':');
            if (separator > 0 &&
                separator < address.Length - 1 &&
                int.TryParse(address[(separator + 1)..], out int parsedPort))
            {
                host = address[..separator];
                port = Math.Clamp(parsedPort, 1, 65535);
            }

            return new ServerEndpoint(host, port);
        }
    }
}
