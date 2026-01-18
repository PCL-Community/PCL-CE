using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Scaffolding.Server.Abstractions;

namespace PCL.Core.Link.Scaffolding.Server.Handlers;

public class GetProtocolsHandler : IRequestHandler
{
    /// <inheritdoc />
    public string RequestType { get; } = "c:protocols";

    /// <inheritdoc />
    public Task<(byte Status, ReadOnlyMemory<byte> Body)> HandleAsync(ReadOnlyMemory<byte> requestBody,
        IServerContext context, string sessionId, CancellationToken ct)
    {
        string[] protocols =
        [
            "c:ping",
            "c:protocols",
            "c:server_port",
            "c:player_ping",
            "c:player_profiles_list"
        ];

        var rseponseContent = string.Join('\0', protocols);
        var responseBody = Encoding.ASCII.GetBytes(rseponseContent);

        return Task.FromResult(((byte)0, new ReadOnlyMemory<byte>(responseBody)));
    }
}