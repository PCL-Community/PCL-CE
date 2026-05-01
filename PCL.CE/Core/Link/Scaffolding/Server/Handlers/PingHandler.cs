using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.CE.Core.Link.Scaffolding.Server.Abstractions;

namespace PCL.CE.Core.Link.Scaffolding.Server.Handlers;

public class PingHandler : IRequestHandler
{
    /// <inheritdoc />
    public string RequestType { get; } = "c:ping";

    /// <inheritdoc />
    public Task<(byte Status, ReadOnlyMemory<byte> Body)> HandleAsync(ReadOnlyMemory<byte> requestBody,
        IServerContext context, string sessionId, CancellationToken ct)
    {
        return Task.FromResult((Status: (byte)0, Body: requestBody));
    }
}