using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.Link.Scaffolding.Client.Abstractions;
using PCL.Core.Link.Scaffolding.Client.Models;

namespace PCL.Core.Link.Scaffolding.Client.Requests;

public sealed class PlayerPingRequest(string name, string machineId, string vendor) : IRequest<bool>
{
    private readonly PlayerProfile _profile = new()
    {
        Name = name,
        MachineId = machineId,
        Vendor = vendor
    };

    private static readonly JsonSerializerOptions _JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    /// <inheritdoc />
    public string RequestType { get; } = "c:player_ping";

    /// <inheritdoc />
    public void WriteRequestBody(IBufferWriter<byte> writer)
    {
        using var jsonWriter = new Utf8JsonWriter(writer);
        JsonSerializer.Serialize(jsonWriter, _profile, _JsonOptions);
    }

    /// <inheritdoc />
    public bool ParseResponseBody(ReadOnlyMemory<byte> responseBody)
    {
        // An empty response body indicates success.
        return responseBody.IsEmpty;
    }
}