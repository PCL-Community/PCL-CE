namespace PCL.Core.Link.Lobby;

/// <summary>
/// Lobby server state.
/// </summary>
public enum LobbyState
{
    /// <summary>
    /// The lobby is idle and not in use.
    /// </summary>
    Idle,

    /// <summary>
    /// The lobby is being initialized.
    /// </summary>
    Initializing,

    /// <summary>
    /// The lobby has been initialized.
    /// </summary>
    Initialized,

    /// <summary>
    /// The lobby is in the process of discovering available minecraft world.
    /// </summary>
    Discovering,

    /// <summary>
    /// The lobby is in the process of creating a new lobby.
    /// </summary>
    Creating,

    /// <summary>
    /// The lobby is in the process of joining a exist lobby.
    /// </summary>
    Joining,

    /// <summary>
    /// The lobby has been joined a exist lobby.
    /// </summary>
    Connected,

    /// <summary>
    /// The lobby is leaving a exist lobby.
    /// </summary>
    Leaving,

    /// <summary>
    /// Occurred an error in the lobby.
    /// </summary>
    Error
}