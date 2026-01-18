using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Link.Scaffolding.Client.Abstractions;

namespace PCL.Core.Link.Scaffolding.Client.Framing;

internal static class ProtocolWriter
{
    /// <summary>
    /// Write message to server.
    /// </summary>
    /// <typeparam name="T">Request body object type.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if request type is too long.</exception>
    /// <exception cref="OperationCanceledException">Thrown if operation is canceled.</exception>
    public static async ValueTask WriteRequestAsync<T>(
        PipeWriter writer,
        IRequest<T> request,
        CancellationToken ct = default)
    {
        var bodyWriter = new ArrayBufferWriter<byte>();
        request.WriteRequestBody(bodyWriter);
        var requestBody = bodyWriter.WrittenMemory;

        var requestTypeBytes = Encoding.ASCII.GetBytes(request.RequestType);
        if (requestTypeBytes.Length > byte.MaxValue)
        {
            throw new InvalidOperationException("Request type is too long.");
        }

        writer.GetSpan(1)[0] = (byte)requestTypeBytes.Length;
        writer.Advance(1);

        writer.Write(requestTypeBytes);

        var lengthSpan = writer.GetSpan(4);
        BinaryPrimitives.WriteUInt32BigEndian(lengthSpan, (uint)requestBody.Length);
        writer.Advance(4);

        if (!requestBody.IsEmpty)
        {
            writer.Write(requestBody.Span);
        }

        var result = await writer.FlushAsync(ct).ConfigureAwait(false);
        if (result.IsCanceled)
        {
            throw new OperationCanceledException();
        }
    }
}