using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Scaffolding.Client.Abstractions;
using PCL.Core.Link.Scaffolding.Exceptions;

namespace PCL.Core.Link.Scaffolding.Client.Framing;

internal static class ProtocolReader
{
    /// <summary>
    /// Read a response from the pipe.
    /// </summary>
    /// <param name="reader">The PipeReader to read from.</param>
    /// <param name="ct">The CancellationToken.</param>
    /// <returns>A task representing the asynchronous operation, with a ScaffoldingResponse as the result.</returns>
    /// <exception cref="ScaffoldingRequestException">Thrown if the request fails due to an invalid status.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the connection is closed unexpectedly.</exception>
    public static async ValueTask<ScaffoldingResponse> ReadResponseAsync(
        PipeReader reader,
        CancellationToken ct = default)
    {
        while (true)
        {
            var result = await reader.ReadAsync(ct).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (_TryParseResponse(ref buffer, out var response))
            {
                reader.AdvanceTo(buffer.Start);
                return response;
            }

            reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);

            if (result.IsCompleted)
            {
                throw new InvalidOperationException("Connection closed unexpectedly.");
            }
        }
    }

    /// <summary>
    /// Tries to parse a response from the given buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing the response data.</param>
    /// <param name="response">The parsed ScaffoldingResponse.</param>
    /// <returns>True if the response was successfully parsed; otherwise, false.</returns>
    /// <exception cref="ScaffoldingRequestException">Thrown if the request fails due to an invalid status.</exception>
    private static bool _TryParseResponse(ref ReadOnlySequence<byte> buffer, out ScaffoldingResponse response)
    {
        response = null!;
        if (buffer.Length < 5)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[5];
        buffer.Slice(0, 5).CopyTo(header);

        var status = header[0];
        var bodyLength = BinaryPrimitives.ReadUInt32BigEndian(header[1..]);

        var fullPacketLength = 5 + bodyLength;
        if (buffer.Length < fullPacketLength)
        {
            return false;
        }

        var bodyBuffer = buffer.Slice(5, bodyLength);
        var body = bodyBuffer.ToArray();

        buffer = buffer.Slice(fullPacketLength);

        response = new ScaffoldingResponse(status, body);

        if (response.Status != 0)
        {
            var serverMessage = response.Status == 255 ? Encoding.UTF8.GetString(response.Body.Span) : null;
            throw new ScaffoldingRequestException(response.Status, serverMessage);
        }

        return true;
    }
}