using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using PCL.Core.Link.Scaffolding.Client.Abstractions;

namespace PCL.Core.Link.Scaffolding.Client.Requests;

public sealed class GetProtocolsRequest(IEnumerable<string> supportProtocols) : IRequest<IReadOnlyList<string>>
{
    private static readonly byte[] _Separator = [(byte)'\0'];
    private readonly IEnumerable<string> _supportProtocols = supportProtocols;

    /// <inheritdoc />
    public string RequestType { get; } = "c:protocols";

    /// <inheritdoc />
    public void WriteRequestBody(IBufferWriter<byte> writer)
    {
        var protocolStr = string.Join('\0', _supportProtocols);
        var bytes = Encoding.ASCII.GetBytes(protocolStr);

        writer.Write(bytes);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ParseResponseBody(ReadOnlyMemory<byte> responseBody)
    {
        if (responseBody.IsEmpty)
        {
            return ArraySegment<string>.Empty;
        }

        var responseStr = Encoding.ASCII.GetString(responseBody.Span);
        return responseStr.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}