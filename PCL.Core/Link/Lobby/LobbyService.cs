using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Link.McPing;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.Utils;

namespace PCL.Core.Link.Lobby;

/// <summary>
///     Lobby server. For auto-management
/// </summary>
[LifecycleService(LifecycleState.Loaded)]
public class LobbyService() : GeneralService("lobby", "LobbyService")
{
    private static readonly LobbyController _LobbyController = new();
    private static CancellationTokenSource _lobbyCts = new();

    private static Task? _discoveringTask;
    private static CancellationTokenSource _discoveringCts = new();

    private static readonly Timer _ServerGameWatcher =
        new(_CheckGameState, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

    private static Action? _serverStoppedHandler;
    private static Action<Exception?>? _serverExceptionHandler;

    private static bool _isGameWatcherRunnable;
    private static int _isLeaving;

    /// <summary>
    ///     Current lobby state.
    /// </summary>
    public static LobbyState CurrentState { get; private set; } = LobbyState.Idle;


    /// <summary>
    ///     Founded local Minecraft worlds.
    /// </summary>
    public static ObservableCollection<FoundWorld> DiscoveredWorlds { get; } = [];

    /// <summary>
    ///     Current players in current lobby.
    /// </summary>
    public static ObservableCollection<PlayerProfile> Players { get; } = [];

    /// <summary>
    ///     Demonstrate whether the current user is the host of the lobby.
    /// </summary>
    public static bool IsHost => _LobbyController.IsHost;

    /// <summary>
    ///     Current lobby full code.
    /// </summary>
    public static string? CurrentLobbyCode { get; private set; }

    /// <summary>
    ///     Current lobby username.
    /// </summary>
    public static string? CurrentUserName { get; private set; }

    /// <summary>
    ///     当前加入者所连接主机的 EasyTier 信息；主机模式或尚未连接时为 <c>null</c>。
    /// </summary>
    public static EasyPlayerInfo? CurrentHostInfo => _LobbyController.ScfClientEntity?.HostInfo;

    /// <inheritdoc />
    public override void Stop()
    {
        _lobbyCts.Cancel();
        _LobbyController.CloseAsync().GetAwaiter().GetResult();
        _ServerGameWatcher.Dispose();
        _lobbyCts.Dispose();

        _discoveringCts.Cancel();
        if (_discoveringTask is not null)
        {
            Task.WhenAll(_discoveringTask);
            _discoveringTask.Dispose();
        }

        _discoveringCts.Dispose();
    }

    private static bool _IsEasyTierMissing()
    {
        return !EasyTierMetadata.IsInstalled;
    }


    public static async Task InitializeAsync()
    {
        if (CurrentState is not LobbyState.Idle && CurrentState is not LobbyState.Error) return;

        _SetState(LobbyState.Initializing);
        try
        {
            if (_IsEasyTierMissing())
            {
                LogWrapper.Info("LobbyService", "EasyTier not found, starting download.");
                OnNeedDownloadEasyTier?.Invoke();
            }
            else
            {
                LogWrapper.Info("LobbyService", "EasyTier files check completed.");
            }

            // refresh naid token
            var naidRefreshToken = States.Link.NaidRefreshToken;
            if (!string.IsNullOrWhiteSpace(naidRefreshToken))
            {
                var expTime = States.Link.NaidRefreshExpireTime;
                if (!string.IsNullOrWhiteSpace(expTime)
                    && DateTime.TryParse(expTime, out var expireAt)
                    && expireAt.CompareTo(DateTime.Now) < 0)
                {
                    States.Link.NaidRefreshToken = string.Empty;
                    NatayarkProfileManager.ResetProfile();
                    HintWrapper.Show(
                        Lang.Text("Tools.GameLink.Natayark.TokenExpired"),
                        HintTheme.Error);
                }
                else
                {
                    try
                    {
                        await NatayarkProfileManager
                            .GetNaidDataAsync(naidRefreshToken, true)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(ex, "LobbyService", "刷新 Natayark 登录信息失败，需要重新登录");
                    }
                }
            }

            _SetState(LobbyState.Initialized);
            LogWrapper.Info("LobbyService", "Lobby service initialized successfully.");

            _ = DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Lobby service initialization failed.");
            HintWrapper.Show(Lang.Text("Link.Lobby.InitFailed"), HintTheme.Error);
            _SetState(LobbyState.Error);
        }
    }

    /// <summary>
    ///     Discover minecraft shared world.
    /// </summary>
    public static async Task DiscoverWorldAsync()
    {
        if (_discoveringCts.IsCancellationRequested) return;

        if (_discoveringTask is { IsCompleted: false }) return;

        if (CurrentState is not LobbyState.Initialized
            && CurrentState is not LobbyState.Idle) return;

        _SetState(LobbyState.Discovering);
        await _RunInUiAsync(() => DiscoveredWorlds.Clear()).ConfigureAwait(false);

        _discoveringTask = Task.Run(
            async () =>
            {
                var recordedPorts = new ConcurrentSet<int>();
                using var listener = new BroadcastListener();

                var handler = new Action<BroadcastRecord, IPEndPoint>((info, _) =>
                    Task.Run(async () =>
                    {
                        if (!recordedPorts.TryAdd(info.Address.Port)) return;

                        using var pinger = McPingServiceFactory.CreateService(
                            new IPEndPoint(IPAddress.Loopback, info.Address.Port));
                        using var cts = new CancellationTokenSource(2000);

                        try
                        {
                            var pingRes = await pinger.PingAsync(cts.Token).ConfigureAwait(false);

                            if (pingRes is null)
                                throw new ArgumentNullException(
                                    nameof(pingRes),
                                    "Failed to ping minecraft entity.");

                            var worldName = Lang.Text(
                                "Link.Lobby.WorldNameFormat",
                                pingRes.Description,
                                pingRes.Version.Name,
                                info.Address.Port);
                            await _RunInUiAsync(() =>
                                    DiscoveredWorlds.Add(
                                        new FoundWorld(worldName, info.Address.Port)))
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogWrapper.Error(
                                ex,
                                "LobbyService",
                                $"Pinging port {info.Address.Port} failed.");
                        }
                    }));

                listener.OnReceive += handler;
                listener.Start();
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                listener.OnReceive -= handler;
            },
            _discoveringCts.Token);

        _SetState(LobbyState.Initialized);
    }

    private static bool _NotHaveNaid()
    {
        return LobbyInfoProvider.RequiresLogin
            && string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.AccessToken);
    }

    /// <summary>
    ///     Create a new lobby.
    /// </summary>
    /// <param name="port">Minecraft share port.</param>
    /// <param name="username">Player name.</param>
    public static async Task<bool> CreateLobbyAsync(int port, string username)
    {
        if (_NotHaveNaid())
        {
            HintWrapper.Show(Lang.Text("Link.Lobby.LoginRequired"), HintTheme.Error);
            return false;
        }

        await _discoveringCts.CancelAsync().ConfigureAwait(false);

        _SetState(LobbyState.Creating);
        try
        {
            CurrentUserName = username;

            var serverEntity = await _LobbyController
                .LaunchServerAsync(username, port)
                .ConfigureAwait(false);
            if (serverEntity is null)
            {
                HintWrapper.Show(Lang.Text("Link.Lobby.CreateRoomFailed"), HintTheme.Error);
                return false;
            }

            CurrentLobbyCode = serverEntity.EasyTier.Lobby.FullCode;

            var stoppedHandler = () => OnServerShutDown?.Invoke();
            var exceptionHandler = _ServerOnServerException;
            _serverStoppedHandler = stoppedHandler;
            _serverExceptionHandler = exceptionHandler;
            serverEntity.Server.ServerStopped += stoppedHandler;
            serverEntity.Server.PlayerProfilePing += _ServerOnPlayerPing;
            serverEntity.Server.ServerStarted += _ServerOnServerStarted;
            serverEntity.Server.ServerException += exceptionHandler;
            serverEntity.Server.Start();

            _SetState(LobbyState.Connected);
            _isGameWatcherRunnable = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Failed to create lobby.");
            HintWrapper.Show(Lang.Text("Link.Lobby.CreateLobbyFailed"), HintTheme.Error);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }

        return true;
    }

    private static void _ServerOnServerException(Exception? ex)
    {
        if (ex is null) return;

        OnServerException?.Invoke(ex);
    }

    private static void _ServerOnServerStarted(IReadOnlyList<PlayerProfile> profiles)
    {
        LogWrapper.Debug("LobbyService", "Send server started event.");
        OnServerStarted?.Invoke();
        _ServerOnPlayerPing(profiles);
    }

    private static void _ServerOnPlayerPing(IReadOnlyList<PlayerProfile> players)
    {
        _ = _RunInUiAsync(() =>
        {
            var currentMachineIds = new HashSet<string>(Players.Select(p => p.MachineId));
            var newMachineIds = new HashSet<string>(players.Select(p => p.MachineId));

            if (currentMachineIds.SetEquals(newMachineIds))
            {
                LogWrapper.Debug("Player list has not changed");
                return; // nothing was changed
            }

            LogWrapper.Debug("LobbyService", "Player list membership has changed, updating UI.");

            var sortedNewPlayers = PlayerListHandler.Sort(players);

            var idsToRemove = currentMachineIds.Except(newMachineIds).ToList();
            if (idsToRemove.Count != 0)
            {
                var playersToRemove =
                    Players.Where(p => idsToRemove.Contains(p.MachineId)).ToList();
                foreach (var player in playersToRemove) Players.Remove(player);
            }

            var idsToAdd = newMachineIds.Except(currentMachineIds).ToList();
            if (idsToAdd.Count != 0)
            {
                var playersToAdd =
                    sortedNewPlayers.Where(p => idsToAdd.Contains(p.MachineId)).ToList();
                foreach (var player in playersToAdd) Players.Add(player);
            }
        });
    }

    /// <summary>
    ///     Join an exist lobby.
    /// </summary>
    /// <param name="lobbyCode">Lobby share code.</param>
    /// <param name="username">Current use name.</param>
    public static async Task<bool> JoinLobbyAsync(string lobbyCode, string username)
    {
        await _discoveringCts.CancelAsync().ConfigureAwait(false);

        _SetState(LobbyState.Joining);

        LogWrapper.Info("LobbyService", $"Try to join lobby {lobbyCode}");

        try
        {
            CurrentUserName = username;
            CurrentLobbyCode = lobbyCode;

            var clientEntity = await _LobbyController
                .LaunchClientAsync(username, lobbyCode, _lobbyCts.Token)
                .ConfigureAwait(false);

            if (clientEntity is null)
                throw new InvalidOperationException(
                    Lang.Text("Link.Lobby.JoinFailed"));

            clientEntity.Client.Heartbeat += _ClientOnHeartbeat;
            clientEntity.Client.ServerShuttedDown += _ClientOnServerShutDown;

            _SetState(LobbyState.Connected);
        }
        catch (ArgumentException codeEx)
        {
            LogWrapper.Error(codeEx, "LobbyService", $"Failed to join lobby {lobbyCode}.");
            HintWrapper.Show(Lang.Text("Link.Lobby.InvalidCodeFormat"), HintTheme.Error);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", $"Failed to join lobby {lobbyCode}.");
            HintWrapper.Show(ex.Message, HintTheme.Error);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }

        return true;
    }

    private static void _ClientOnServerShutDown()
    {
        OnServerShutDown?.Invoke();
    }

    private static void _ClientOnHeartbeat(IReadOnlyList<PlayerProfile> players, long latency)
    {
        _ = _RunInUiAsync(() =>
        {
            var currentMachineIds = new HashSet<string>(Players.Select(p => p.MachineId));
            var newMachineIds = new HashSet<string>(players.Select(p => p.MachineId));

            if (currentMachineIds.SetEquals(newMachineIds)) return; // nothing was changed

            LogWrapper.Debug("LobbyService", "Player list membership has changed, updating UI.");

            var sortedNewPlayers = PlayerListHandler.Sort(players);

            var idsToRemove = currentMachineIds.Except(newMachineIds).ToList();
            if (idsToRemove.Count != 0)
            {
                var playersToRemove =
                    Players.Where(p => idsToRemove.Contains(p.MachineId)).ToList();
                foreach (var player in playersToRemove) Players.Remove(player);
            }

            var idsToAdd = newMachineIds.Except(currentMachineIds).ToList();
            if (idsToAdd.Count != 0)
            {
                var playersToAdd =
                    sortedNewPlayers.Where(p => idsToAdd.Contains(p.MachineId)).ToList();
                foreach (var player in playersToAdd) Players.Add(player);
            }

            OnClientPing?.Invoke(latency);
        });
    }


    /// <summary>
    ///     Leave from lobby.
    /// </summary>
    public static async Task LeaveLobbyAsync()
    {
        if (Interlocked.Exchange(ref _isLeaving, 1) == 1)
            return;

        try
        {
            _SetState(LobbyState.Leaving);

            await _lobbyCts.CancelAsync().ConfigureAwait(false);

            Players.Clear();
            CurrentLobbyCode = null;
            CurrentUserName = null;

            if (_LobbyController.ScfClientEntity?.Client is not null)
            {
                _LobbyController.ScfClientEntity.Client.Heartbeat -= _ClientOnHeartbeat;
                _LobbyController.ScfClientEntity.Client.ServerShuttedDown -=
                    _ClientOnServerShutDown;
            }

            if (_LobbyController.ScfServerEntity?.Server is { } server)
            {
                server.PlayerProfilePing -= _ServerOnPlayerPing;
                server.ServerStarted -= _ServerOnServerStarted;
                var stoppedHandler = _serverStoppedHandler;
                var exceptionHandler = _serverExceptionHandler;
                if (stoppedHandler is not null)
                    server.ServerStopped -= stoppedHandler;
                if (exceptionHandler is not null)
                    server.ServerException -= exceptionHandler;
            }

            _serverStoppedHandler = null;
            _serverExceptionHandler = null;

            await _LobbyController.CloseAsync().ConfigureAwait(false);


            _lobbyCts = new CancellationTokenSource();
            _SetState(LobbyState.Initialized);

            LogWrapper.Info("LobbyService", "Left lobby and cleaned up resources.");

            _isGameWatcherRunnable = false;

            _discoveringTask = null;
            _discoveringCts = new CancellationTokenSource();
            _ = DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Failed when leave lobby.");

            _lobbyCts = new CancellationTokenSource();
            _discoveringTask = null;
            _discoveringCts = new CancellationTokenSource();
            _serverStoppedHandler = null;
            _serverExceptionHandler = null;
            _isGameWatcherRunnable = false;
            Players.Clear();
            CurrentLobbyCode = null;
            CurrentUserName = null;
            _SetState(LobbyState.Initialized);
            throw;
        }
        finally
        {
            _isLeaving = 0;
        }
    }

    private static void _SetState(LobbyState newState)
    {
        var oldState = CurrentState;
        if (oldState == newState) return;

        CurrentState = newState;

        LogWrapper.Info("LobbyService", $"Lobby state changed from {oldState} to {newState}");

        StateChanged?.Invoke(oldState, newState);
    }

    private static void _CheckGameState(object? state)
    {
        if (!_isGameWatcherRunnable) return;

        if (_LobbyController.ScfServerEntity is null) return;

        LobbyController
            .IsHostInstanceAvailableAsync(_LobbyController.ScfServerEntity.EasyTier.MinecraftPort)
            .ContinueWith(async task =>
            {
                var isExist = await task.ConfigureAwait(false);
                if (!isExist)
                {
                    _isGameWatcherRunnable = false;
                    OnUserStopGame?.Invoke();
                }
            });
    }

    private static async Task _RunInUiAsync(Action action)
    {
        if (PlatformServices.UiDispatcher is { } dispatcher)
            await dispatcher.InvokeAsync(action);
    }

    #region UI Events

    /// <summary>
    ///     Invoked when lobby state changed. (first arg is the old state; second arg is the new state.)
    /// </summary>
    public static event Action<LobbyState, LobbyState>? StateChanged;

    /// <summary>
    ///     Invoked when need to download EasyTier core files.
    /// </summary>
    public static event Action? OnNeedDownloadEasyTier;

    /// <summary>
    ///     Invoked when user stop the game in server mode.
    /// </summary>
    public static event Action? OnUserStopGame;

    /// <summary>
    ///     Invoked when client ping happened.
    /// </summary>
    public static event Action<long>? OnClientPing;

    /// <summary>
    ///     Invoked when server shut down.
    /// </summary>
    public static event Action? OnServerShutDown;


    /// <summary>
    ///     Invoked when server started successfully.
    /// </summary>
    public static event Action? OnServerStarted;

    public static event Action<Exception>? OnServerException;

    #endregion
}

/// <summary>
///     Founded minecraft world information.
/// </summary>
/// <param name="Name">World name.</param>
/// <param name="Port">World share port.</param>
public record FoundWorld(string Name, int Port);