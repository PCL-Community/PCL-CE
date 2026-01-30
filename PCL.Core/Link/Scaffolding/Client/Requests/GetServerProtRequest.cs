using System;
using System.Buffers;
using System.Buffers.Binary;
using PCL.Core.Link.Scaffolding.Client.Abstractions;

namespace PCL.Core.Link.Scaffolding.Client.Requests;

public sealed class GetServerPortRequest : IRequest<ushort>
{
    /// <inheritdoc />
    public string RequestType { get; } = "c:server_port";

    /// <inheritdoc />
    public void WriteRequestBody(IBufferWriter<byte> writer)
    {
        // empty
    }

    /// <inheritdoc />
    public ushort ParseResponseBody(ReadOnlyMemory<byte> responseBody)
    {
        if (responseBody.Length != 2)
        {
            throw new InvalidOperationException("Invalid response body for server port.");
        }

        return BinaryPrimitives.ReadUInt16BigEndian(responseBody.Span);
    }
}