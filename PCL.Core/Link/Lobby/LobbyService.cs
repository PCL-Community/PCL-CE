using PCL.Core.App;
using PCL.Core.Link.Natayark;
using PCL.Core.Link.Scaffolding;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PCL.Core.Link.Lobby;

/// <summary>
/// Lobby server. For auto-management
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

    private static bool _isGameWatcherRunnable = false;

    /// <summary>
    /// Current lobby state.
    /// </summary>
    public static LobbyState CurrentState { get; private set; } = LobbyState.Idle;


    /// <summary>
    /// Founded local Minecraft worlds.
    /// </summary>
    public static ObservableCollection<FoundWorld> DiscoveredWorlds { get; } = [];

    /// <summary>
    /// Current players in current lobby.
    /// </summary>
    public static ObservableCollection<PlayerProfile> Players { get; private set; } = [];

    /// <summary>
    /// Demonstrate whether the current user is the host of the lobby.
    /// </summary>
    public static bool IsHost => _LobbyController.IsHost;

    /// <summary>
    /// Current lobby full code.
    /// </summary>
    public static string? CurrentLobbyCode { get; private set; }

    /// <summary>
    /// Current lobby username.
    /// </summary>
    public static string? CurrentUserName { get; private set; }

    #region UI Events

    /// <summary>
    /// Invoked when lobby state changed. (first arg is the old state; second arg is the new state.)
    /// </summary>
    public static event Action<LobbyState, LobbyState>? StateChanged;

    /// <summary>
    /// Used for UI layer to send Hint.
    /// </summary>
    public static event Action<string, CoreHintType>? OnHint;

    /// <summary>
    /// Invoked when need to download EasyTier core files.
    /// </summary>
    public static event Action? OnNeedDownloadEasyTier;

    /// <summary>
    /// Invoked when user stop the game in server mode.
    /// </summary>
    public static event Action? OnUserStopGame;

    /// <summary>
    /// Invoked when client ping happened.
    /// </summary>
    public static event Action<long>? OnClientPing;

    /// <summary>
    /// Invoked when server shut down.
    /// </summary>
    public static event Action? OnServerShutDown;


    /// <summary>
    /// Invoked when server started successfully.
    /// </summary>
    public static event Action? OnServerStarted;

    public static event Action<Exception>? OnServerException;

    #endregion

    /// <inheritdoc />
    public override void Stop()
    {
        _ = _LobbyController.CloseAsync();
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

    private static bool _IsEasyTierCoreFileNotExist() =>
        !File.Exists(Path.Combine(EasyTierMetadata.EasyTierFilePath, "easytier-core.exe")) &&
        !File.Exists(Path.Combine(EasyTierMetadata.EasyTierFilePath, "Packet.dll")) &&
        !File.Exists(Path.Combine(EasyTierMetadata.EasyTierFilePath, "easytier-cli.exe"));


    public static async Task InitializeAsync()
    {
        if (CurrentState is not LobbyState.Idle && CurrentState is not LobbyState.Error)
        {
            return;
        }

        _SetState(LobbyState.Initializing);
        try
        {
            if (_IsEasyTierCoreFileNotExist())
            {
                LogWrapper.Info("LobbyService", "EasyTier not found, starting download.");
                OnNeedDownloadEasyTier?.Invoke();
            }
            else
            {
                LogWrapper.Info("LobbyService", "EasyTier files check completed.");
            }

            // refresh naid token
            var naidRefreshToken = Config.Link.NaidRefreshToken;
            if (!string.IsNullOrWhiteSpace(naidRefreshToken))
            {
                var expTime = Config.Link.NaidRefreshExpireTime;
                if (!string.IsNullOrWhiteSpace(expTime) &&
                    Convert.ToDateTime(expTime).CompareTo(DateTime.Now) < 0)
                {
                    Config.Link.NaidRefreshToken = string.Empty;
                    OnHint?.Invoke("Natayark ID 令牌已过期，请重新登录", CoreHintType.Critical);
                }
                else
                {
                    await NatayarkProfileManager.GetNaidDataAsync(naidRefreshToken, true).ConfigureAwait(false);
                }
            }

            _SetState(LobbyState.Initialized);
            LogWrapper.Info("LobbyService", "Lobby service initialized successfully.");

            _ = DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Lobby service initialization failed.");
            OnHint?.Invoke("大厅服务初始化失败，请检查网络连接。", CoreHintType.Critical);
            _SetState(LobbyState.Error);
        }
    }

    /// <summary>
    /// Discover minecraft shared world.
    /// </summary>
    public static async Task DiscoverWorldAsync()
    {
        if (_discoveringCts.IsCancellationRequested)
        {
            return;
        }

        if (CurrentState is not LobbyState.Initialized && CurrentState is not LobbyState.Idle)
        {
            return;
        }

        _SetState(LobbyState.Discovering);
        await _RunInUiAsync(() => DiscoveredWorlds.Clear()).ConfigureAwait(false);

        _discoveringTask = Task.Run(async () =>
        {
            var recordedPorts = new ConcurrentSet<int>();
            using var listener = new BroadcastListener();

            var handler = new Action<BroadcastRecord, IPEndPoint>(async (info, _) =>
            {
                if (!recordedPorts.TryAdd(info.Address.Port)) return;

                using var pinger = new McPing(new IPEndPoint(IPAddress.Loopback, info.Address.Port));
                using var cts = new CancellationTokenSource(2000);

                try
                {
                    var pingRes = await pinger.PingAsync(cts.Token).ConfigureAwait(false);

                    if (pingRes is null)
                    {
                        throw new ArgumentNullException(nameof(pingRes), "Failed to ping minecraft entity.");
                    }

                    var worldName = $"{pingRes.Description} / {pingRes.Version.Name} ({info.Address.Port})";
                    await _RunInUiAsync(() => DiscoveredWorlds.Add(new FoundWorld(worldName, info.Address.Port)))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "LobbyService", $"Pinging port {info.Address.Port} failed.");
                }
            });

            listener.OnReceive += handler;
            listener.Start();
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            listener.OnReceive -= handler;
        }, _discoveringCts.Token);

        _SetState(LobbyState.Initialized);
    }

    private static bool _NotHaveNaid() =>
        LobbyInfoProvider.RequiresLogin &&
        string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.AccessToken);

    /// <summary>
    /// Create a new lobby.
    /// </summary>
    /// <param name="port">Minecraft share port.</param>
    /// <param name="username">Player name.</param>
    public static async Task<bool> CreateLobbyAsync(int port, string username)
    {
        if (_NotHaveNaid())
        {
            OnHint?.Invoke("请先登录 Natayark ID 再使用大厅！", CoreHintType.Critical);
            return false;
        }

        await _discoveringCts.CancelAsync().ConfigureAwait(false);

        _SetState(LobbyState.Creating);
        try
        {
            CurrentUserName = username;

            var serverEntity = await _LobbyController.LaunchServerAsync(username, port).ConfigureAwait(false);
            if (serverEntity is null)
            {
                OnHint?.Invoke("在创建房间的时候遇到了问题，请查看日志并将此问题反馈给开发者！", CoreHintType.Critical);
                return false;
            }

            CurrentLobbyCode = serverEntity.EasyTier.Lobby.FullCode;

            serverEntity.Server.ServerStopped += () => OnServerShutDown?.Invoke();
            serverEntity.Server.PlayerProfilePing += _ServerOnPlayerPing;
            serverEntity.Server.ServerStarted += _ServerOnServerStarted;
            serverEntity.Server.ServerException += _ServerOnServerException;
            //serverEntity.EasyTier.EasyTierProcessExisted += () =>
            //{
            //    OnHint?.Invoke("EasyTierCore异常退出", CoreHintType.Critical);
            //    OnServerShutDown?.Invoke();
            //}; this code will be invoked when EasyTier process successfully exited, not in failed state.

            serverEntity.Server.Start();

            _SetState(LobbyState.Connected);
            _isGameWatcherRunnable = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Failed to create lobby.");
            OnHint?.Invoke("创建大厅失败，请检查日志或向开发者反馈。", CoreHintType.Critical);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }

        return true;
    }

    private static void _ServerOnServerException(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

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
            if (idsToRemove.Any())
            {
                var playersToRemove = Players.Where(p => idsToRemove.Contains(p.MachineId)).ToList();
                foreach (var player in playersToRemove)
                {
                    Players.Remove(player);
                }
            }

            var idsToAdd = newMachineIds.Except(currentMachineIds).ToList();
            if (idsToAdd.Any())
            {
                var playersToAdd = sortedNewPlayers.Where(p => idsToAdd.Contains(p.MachineId)).ToList();
                foreach (var player in playersToAdd)
                {
                    Players.Add(player);
                }
            }
        });
    }

    /// <summary>
    /// Join an exist lobby.
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

            var clientEntity = await _LobbyController.LaunchClientAsync(username, lobbyCode).ConfigureAwait(false);

            if (clientEntity is null)
            {
                throw new InvalidOperationException(
                    "加入大厅失败，可能是大厅不存在或已被解散");
            }

            clientEntity.Client.Heartbeat += _ClientOnHeartbeat;
            clientEntity.Client.ServerShuttedDown += _ClientOnServerShutDown;

            _SetState(LobbyState.Connected);
        }
        catch (ArgumentException codeEx)
        {
            LogWrapper.Error(codeEx, "LobbyService", $"Failed to join lobby {lobbyCode}.");
            OnHint?.Invoke("大厅编号不正确，请检查后再试！", CoreHintType.Critical);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", $"Failed to join lobby {lobbyCode}.");
            OnHint?.Invoke(ex.Message, CoreHintType.Critical);
            await LeaveLobbyAsync().ConfigureAwait(false);

            return false;
        }

        return true;
    }

    private static void _ClientOnServerShutDown()
    {
        OnServerShutDown?.Invoke();

        _ = LeaveLobbyAsync();
    }

    private static void _ClientOnHeartbeat(IReadOnlyList<PlayerProfile> players, long latency)
    {
        _ = _RunInUiAsync(() =>
        {
            var currentMachineIds = new HashSet<string>(Players.Select(p => p.MachineId));
            var newMachineIds = new HashSet<string>(players.Select(p => p.MachineId));

            if (currentMachineIds.SetEquals(newMachineIds))
            {
                return; // nothing was changed
            }

            LogWrapper.Debug("LobbyService", "Player list membership has changed, updating UI.");

            var sortedNewPlayers = PlayerListHandler.Sort(players);

            var idsToRemove = currentMachineIds.Except(newMachineIds).ToList();
            if (idsToRemove.Any())
            {
                var playersToRemove = Players.Where(p => idsToRemove.Contains(p.MachineId)).ToList();
                foreach (var player in playersToRemove)
                {
                    Players.Remove(player);
                }
            }

            var idsToAdd = newMachineIds.Except(currentMachineIds).ToList();
            if (idsToAdd.Any())
            {
                var playersToAdd = sortedNewPlayers.Where(p => idsToAdd.Contains(p.MachineId)).ToList();
                foreach (var player in playersToAdd)
                {
                    Players.Add(player);
                }
            }

            OnClientPing?.Invoke(latency);
        });
    }


    /// <summary>
    /// Leave from lobby.
    /// </summary>
    public static async Task LeaveLobbyAsync()
    {
        _SetState(LobbyState.Leaving);

        try
        {
            await _lobbyCts.CancelAsync().ConfigureAwait(false);

            Players.Clear();
            CurrentLobbyCode = null;
            CurrentUserName = null;

            if (_LobbyController.ScfClientEntity?.Client != null)
            {
                _LobbyController.ScfClientEntity.Client.Heartbeat -= _ClientOnHeartbeat;
            }

            if (_LobbyController.ScfServerEntity?.Server != null)
            {
                _LobbyController.ScfServerEntity.Server.PlayerProfilePing -= _ServerOnPlayerPing;
                _LobbyController.ScfServerEntity.Server.ServerStarted -= _ServerOnServerStarted;
            }

            await _LobbyController.CloseAsync().ConfigureAwait(false);


            _lobbyCts = new CancellationTokenSource();
            _SetState(LobbyState.Initialized);

            LogWrapper.Info("LobbyService", "Left lobby and cleaned up resources.");

            _isGameWatcherRunnable = false;

            _discoveringCts = new CancellationTokenSource();
            _ = DiscoverWorldAsync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "LobbyService", "Failed when leave lobby.");
        }
    }

    private static void _SetState(LobbyState newState)
    {
        var oldState = CurrentState;
        if (oldState == newState)
        {
            return;
        }

        CurrentState = newState;

        LogWrapper.Info("LobbyService", $"Lobby state changed from {oldState} to {newState}");

        StateChanged?.Invoke(oldState, newState);
    }

    private static void _CheckGameState(object? state)
    {
        if (!_isGameWatcherRunnable)
        {
            return;
        }

        if (_LobbyController.ScfServerEntity is null)
        {
            return;
        }

        LobbyController.IsHostInstanceAvailableAsync(_LobbyController.ScfServerEntity.EasyTier.MinecraftPort)
            .ContinueWith(async (task) =>
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
        await Application.Current.Dispatcher.InvokeAsync(action);
    }
}

/// <summary>
/// Founded minecraft world information.
/// </summary>
/// <param name="Name">World name.</param>
/// <param name="Port">World share port.</param>
public record FoundWorld(string Name, int Port);

/// <summary>
/// Hint type in PCL.Core (for UI display).
/// </summary>
public enum CoreHintType
{
    /// <summary>
    /// 信息，通常是蓝色的“i”。
    /// </summary>
    /// <remarks></remarks>
    Info,

    /// <summary>
    /// 已完成，通常是绿色的“√”。
    /// </summary>
    /// <remarks></remarks>
    Finish,

    /// <summary>
    /// 错误，通常是红色的“×”。
    /// </summary>
    /// <remarks></remarks>
    Critical
}