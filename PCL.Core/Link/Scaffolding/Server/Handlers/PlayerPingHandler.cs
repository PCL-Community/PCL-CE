using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.Server.Abstractions;
using PCL.Core.Logging;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.Scaffolding.Server.Handlers;

public class PlayerPingHandler : IRequestHandler
{
    /// <inheritdoc />
    public string RequestType { get; } = "c:player_ping";

    private static readonly JsonSerializerOptions _JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public Task<(byte Status, ReadOnlyMemory<byte> Body)> HandleAsync(ReadOnlyMemory<byte> requestBody,
        IServerContext context, string sessionId, CancellationToken ct)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<PlayerProfile>(requestBody.Span, _JsonOptions);
            if (profile is null || string.IsNullOrEmpty(profile.MachineId))
            {
                LogWrapper.Warn("ScaffoldingServer",
                    $"Received a player_ping from session {sessionId} with a missing or empty machine_id. Ignoring.");
                return Task.FromResult(((byte)32, ReadOnlyMemory<byte>.Empty));
            }

            var guestProfile = profile with { Kind = PlayerKind.GUEST };

            var listChanged = false;
            context.TrackedPlayers.AddOrUpdate(guestProfile.MachineId,
                _ =>
                {
                    LogWrapper.Info("ScaffoldingServer",
                        $"New player '{guestProfile.Name}' with machine_id '{guestProfile.MachineId}' connected.");
                    var newPlayer = new TrackedPlayerProfile { Profile = profile, LastSeenUtc = DateTime.UtcNow };
                    listChanged = true;
                    return newPlayer;
                },
                (_, existingPlayer) =>
                {
                    existingPlayer.Profile = guestProfile;
                    existingPlayer.LastSeenUtc = DateTime.UtcNow;

                    return existingPlayer;
                });

            if (listChanged)
            {
                context.OnPlayerProfilesChanged();
            }
            
            return Task.FromResult(((byte)0, ReadOnlyMemory<byte>.Empty));
        }
        catch (JsonException ex)
        {
            LogWrapper.Warn("ScaffoldingServer",
                $"Failed to deserialize player_ping JSON from session {sessionId}. Error: {ex.Message}");
            return Task.FromResult(((byte)32, ReadOnlyMemory<byte>.Empty));
        }
    }
}