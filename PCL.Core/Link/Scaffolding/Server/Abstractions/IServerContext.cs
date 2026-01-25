using PCL.Core.Link.Scaffolding.Client.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PCL.Core.Link.Scaffolding.Server.Abstractions;

public interface IServerContext
{
    /// <summary>
    /// Get the list of currently connected player profiles.
    /// </summary>
    IReadOnlyList<PlayerProfile> PlayerProfiles { get; }

    /// <summary>
    /// Gets the list of currently connected player profiles, keyed by a unique session identifier.
    /// </summary>
    ConcurrentDictionary<string, TrackedPlayerProfile> TrackedPlayers { get; }

    /// <summary>
    /// Occurs on player profile changed.
    /// </summary>
    void OnPlayerProfilesChanged();

    /// <summary>
    /// Occurs when the list of player profiles changes.
    /// </summary>
    event Action<IReadOnlyList<PlayerProfile>> PlayerProfilesPing;

    /// <summary>
    /// Gets the prot of the running Minecraft server.
    /// </summary>
    int MinecraftServerProt { get; }


    /// <summary>
    /// Gets the room information.
    /// </summary>
    LobbyInfo UserLobbyInfo { get; }

    /// <summary>
    /// Player name(Host).
    /// </summary>
    string PlayerName { get; }
}