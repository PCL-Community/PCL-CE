using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Scaffolding.Server.Abstractions;

public interface IRequestHandler
{
    /// <summary>
    /// Gets the request type this handler is responsible for, e.g., "c:ping".
    /// </summary>
    string RequestType { get; }

    /// <summary>
    /// Handle an incoming request and returns a response.
    /// </summary>
    /// <param name="requestBody">The raw body of the request.</param>
    /// <param name="context">The shared server context.</param>
    /// <param name="sessionId">A unique identifier for the client session.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A tuple containing the status code and the response body.</returns>
    Task<(byte Status, ReadOnlyMemory<byte> Body)> HandleAsync
        (ReadOnlyMemory<byte> requestBody, IServerContext context, string sessionId, CancellationToken ct);
}