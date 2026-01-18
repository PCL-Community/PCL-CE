using System;
using System.Buffers;

namespace PCL.Core.Link.Scaffolding.Client.Abstractions;

public interface IRequest<out TResponse>
{
    /// <summary>
    /// Gets the request type string, e.g., "c:ping".
    /// </summary>
    string RequestType { get; }

    /// <summary>
    /// Writes the request body to the provided buffer writer.
    /// </summary>
    /// <param name="writer">The buffer to write to.</param>
    void WriteRequestBody(IBufferWriter<byte> writer);

    /// <summary>
    /// Parses the response body from the given memory span.
    /// </summary>
    /// <param name="responseBody">The raw response body.</param>
    /// <returns>The parsed response object.</returns>
    TResponse ParseResponseBody(ReadOnlyMemory<byte> responseBody);
}