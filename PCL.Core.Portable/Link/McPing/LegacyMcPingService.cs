// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Logging;

namespace PCL.Core.Link.McPing;

/// <summary>
/// Minecraft 1.6 and earlier legacy status protocol client.
/// </summary>
public class LegacyMcPingService : IMcPingService
{
    private const int DefaultTimeout = 10_000;
    private const string ModuleName = "LegacyMcPing";
    private readonly IPEndPoint _endpoint;
    private readonly string _host;
    private readonly int _timeout;
    private bool _disposed;

    public IPEndPoint Endpoint => _endpoint;
    public string Host => _host;
    public int Timeout => _timeout;

    public LegacyMcPingService(IPEndPoint endpoint, int timeout = DefaultTimeout)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, 0);
        _endpoint = endpoint;
        _host = endpoint.Address.ToString();
        _timeout = timeout;
    }

    public LegacyMcPingService(string host, int port = 25565, int timeout = DefaultTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if ((uint)port > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(port));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, 0);
        _host = host;
        _timeout = timeout;
        if (!IPAddress.TryParse(host, out var address))
        {
            var addresses = Dns.GetHostAddresses(host);
            address = Array.Find(addresses, candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                      ?? addresses.FirstOrDefault()
                      ?? throw new SocketException((int)SocketError.HostNotFound);
        }
        _endpoint = new IPEndPoint(address, port);
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
            await stream.WriteAsync(new byte[] { 0xFE, 0x01 }, linkedCts.Token).ConfigureAwait(false);

            var response = new ArrayBufferWriter<byte>(512);
            var rented = ArrayPool<byte>.Shared.Rent(512);
            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(rented.AsMemory(), linkedCts.Token).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    response.Write(rented.AsSpan(0, read));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }

            return ParseResponse(response.WrittenSpan);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            PortableLog.Warn(ex, ModuleName, $"Legacy server query timed out: {_endpoint}");
            return null;
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException)
        {
            PortableLog.Warn(ex, ModuleName, $"Legacy server query failed: {_endpoint}");
            return null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static McPingResult ParseResponse(ReadOnlySpan<byte> response)
    {
        if (response.Length < 3 || response[0] != 0xFF)
            throw new InvalidDataException("The legacy Minecraft response is invalid.");

        var characterCount = BinaryPrimitives.ReadUInt16BigEndian(response.Slice(1, 2));
        var byteCount = checked(characterCount * 2);
        if (response.Length - 3 < byteCount)
            throw new InvalidDataException("The legacy Minecraft response is truncated.");

        var payload = Encoding.BigEndianUnicode.GetString(response.Slice(3, byteCount));
        var fields = payload.Split('\0');
        if (fields.Length < 6 || fields[0] != "§1")
            throw new InvalidDataException("The legacy Minecraft response has an unknown format.");

        return new McPingResult(
            new McPingVersionResult(fields[2], ParseInt32(fields[1])),
            new McPingPlayerResult(ParseInt32(fields[5]), ParseInt32(fields[4]), []),
            fields[3],
            null,
            0,
            null,
            null);
    }

    private static int ParseInt32(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
