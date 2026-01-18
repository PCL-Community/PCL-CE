using PCL.Core.App;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Logging;
using PCL.Core.Net;
using PCL.Core.Utils;
using Polly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Net.Http.Client;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
/// Demonstrates the state of EasyTier entity.
/// </summary>
public enum EtState
{
    Stopped,
    Active,
    Ready
}

/// <summary>
/// An EasyTier entity that manages the EasyTier process and its interactions.
/// </summary>
public class EasyTierEntity
{
    private readonly Process _etProcess;
    private readonly int _rpcPort;
    private readonly LobbyInfo _lobby;
    private readonly int _scfPort;

    public int ForwardPort { get; private set; }
    public int MinecraftPort { get; init; }
    public EtState State { get; private set; }
    public LobbyInfo Lobby => _lobby;

    public event Action? EasyTierProcessExisted;

    /// <summary>
    /// Constructor of EasyTierEntity
    /// </summary>
    /// <param name="lobby">The room information.</param>
    /// <param name="minecraftPort">Minecraft port.</param>
    /// <param name="scfPort">The server port.</param>
    /// <param name="asHost">Indicates whether the entity acts as a host.</param>
    /// <exception cref="FileNotFoundException">Thrown if EasyTier was broken.</exception>
    public EasyTierEntity(LobbyInfo lobby, int minecraftPort, int scfPort, bool asHost)
    {
        _lobby = lobby;
        MinecraftPort = minecraftPort;
        _scfPort = scfPort;
        State = EtState.Stopped;

        var existEntities = Process.GetProcessesByName("easytier-core");
        foreach (var entity in existEntities)
        {
            LogWrapper.Warn("EasyTier", $"Find exist EasyTier Entity, may affect something: {entity.Id}");
        }

        LogWrapper.Info("EasyTier", $"EasyTier folder path: {EasyTierMetadata.EasyTierFilePath}");

        if (!(File.Exists($"{EasyTierMetadata.EasyTierFilePath}\\easytier-core.exe") &&
              File.Exists($"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe") &&
              File.Exists($"{EasyTierMetadata.EasyTierFilePath}\\Packet.dll")))
        {
            LogWrapper.Error("EasyTier", "EasyTier was broken.");

            throw new FileNotFoundException("EasyTier was broken.");
        }

        State = EtState.Ready;

        _rpcPort = NetworkHelper.NewTcpPort();

        ForwardPort = NetworkHelper.NewTcpPort();

        _etProcess = _BuildProcessAsync(asHost).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Launches EasyTier process.
    /// </summary>
    /// <returns>
    /// - 1 means failed to launch EasyTier and can never launch again.<br/>
    /// - 0 means successful launch.
    /// </returns>
    public int Launch()
    {
        LogWrapper.Info("EasyTier", "Launch EasyTier Core.");

        try
        {
            // LogWrapper.Info("Test", _etProcess.StartInfo.Arguments);
            _etProcess.Start();
            State = EtState.Active;

            var cli = _GetCliOutputDebug();
            
            _etProcess.Exited += (_, _) => EasyTierProcessExisted?.Invoke();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "EasyTier", "Failed to launch EasyTier.");
            State = EtState.Stopped;
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Stops EasyTier process.
    /// </summary>
    /// <returns>
    /// - 1 means failed to stop EasyTier.<br/>
    /// - 0 means successful stop.
    /// </returns>
    public Task<int> StopAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!_etProcess.HasExited)
                {
                    _etProcess.Kill(true);
                    _etProcess.WaitForExit(5000);
                }

                State = EtState.Stopped;
                return 0;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "EasyTier", "Failed to stop EasyTier.");
                State = EtState.Stopped;
                return 1;
            }
        });
    }

    private async Task<Process> _BuildProcessAsync(bool asHost)
    {
        var process = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-core.exe",
                WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        var args = new ArgumentsBuilder();

        args.AddFlag("no-tun")
            .AddFlag("multi-thread")
            .AddFlag("enable-kcp-proxy")
            .AddFlag("enable-quic-proxy")
            .AddFlagIf(!Config.Link.TryPunchSym, "disable-sys-hole-punching")
            .AddFlagIf(!Config.Link.EnableIPv6, "disable-ipv6")
            .AddFlagIf(Config.Link.LatencyFirstMode, "latency-first")
            .Add("encryption-algorithm", "aes-gcm")
            .Add("compression", "zstd")
            .Add("default-protocol", Config.Link.ProtocolPreference.ToString().ToLowerInvariant())
            .Add("network-name", _lobby.NetworkName)
            .Add("network-secret", _lobby.NetworkSecret)
            //.Add("relay-network-whitelist", _lobby.NetworkName)
            .Add("machine-id", Utils.Secret.Identify.LauncherId)
            .Add("rpc-portal", _rpcPort.ToString())
            .Add("private-mode", "true")
            .AddFlag("p2p-only");


        if (asHost)
        {
            args.AddWithSpace("i", "10.114.51.41")
                .Add("hostname", $"scaffolding-mc-server-{_scfPort}")
                .Add("tcp-whitelist", _scfPort.ToString())
                .Add("udp-whitelist", _scfPort.ToString())
                .Add("tcp-whitelist", MinecraftPort.ToString())
                .Add("udp-whitelist", MinecraftPort.ToString())
                .Add("l", "tcp://0.0.0.0:0")
                .Add("l", "udp://0.0.0.0:0");
        }
        else
        {
            args.AddFlag("d")
                .Add("hostname", Guid.NewGuid().ToString())
                .Add("tcp-whitelist", "0")
                .Add("udp-whitelist", "0")
                .Add("l", "tcp://0.0.0.0:0")
                .Add("l", "udp://0.0.0.0:0");
        }

        foreach (var address in _fallbackNodeLinks)
        {
            args.Add("p", address);
        }
        
        // foreach (var address in await _GetEtRelayListAsync().ConfigureAwait(false))
        // {
        //     args.Add("p", address);
        // }

        // if (Config.Link.RelayType == 1)
        // {
        //     args.AddFlag("disable-p2p");
        // }

        process.StartInfo.Arguments = args.GetResult();

        LogWrapper.Debug("EasyTier", process.StartInfo.Arguments);

        return process;
    }

    private async Task<IReadOnlyList<string>> _GetEtRelayListAsync()
    {
        var relays = ETRelay.RelayList;
        var customedNodes = Config.Link.RelayServer.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var node in customedNodes)
        {
            if (node.Contains("tcp://", StringComparison.OrdinalIgnoreCase) ||
                node.Contains("udp://", StringComparison.OrdinalIgnoreCase))
            {
                relays.Add(new ETRelay
                {
                    Url = node,
                    Name = "Custom",
                    Type = ETRelayType.Custom
                });
            }
            else
            {
                LogWrapper.Warn("EasyTier", $"Invalid custom node URL: {node}.");
            }
        }

        var setupRelayList = relays.Select(relay => new { relay, serverType = Config.Link.ServerType })
            .Where(rl =>
                (rl.relay.Type == ETRelayType.Selfhosted && rl.serverType != 2) ||
                (rl.relay.Type == ETRelayType.Community && rl.serverType == 1) ||
                rl.relay.Type == ETRelayType.Custom)
            .Select(rl => rl.relay.Url).ToImmutableList();

        var pubNode = await _GetPublicNodeAsync().ConfigureAwait(false);

        var result = setupRelayList
            .Concat(pubNode)
            .Take(6)
            .ToImmutableList();

        LogWrapper.Debug($"Get public node:\n{string.Join("\n\t", result)}");

        return result;
    }

    private readonly string[] _fallbackNodeLinks =
    [
        "tcp://public.easytier.top:11010",
        "tcp://public2.easytier.cn:54321",
        "https://etnode.zkitefly.eu.org/node1",
        "https://etnode.zkitefly.eu.org/node2"
    ];


    private async Task<IReadOnlyList<string>> _GetPublicNodeAsync()
    {
        var rep = await Policy.Handle<HttpRequestException>()
            .OrResult<HttpResponseHandler>(msg => !msg.IsSuccess)
            .WaitAndRetryAsync(4, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
            .ExecuteAsync(_SendPublicNodeGetReqAsync).ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(rep);

        var content = await rep.AsStringAsync().ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize<PublicNodeDto>(content);

        ArgumentNullException.ThrowIfNull(dto);

        var result = dto.Data.Items
            .Where(it => it is { IsActive: true, IsAllowRelay: true })
            .Select(it => it.Host)
            .Union(_fallbackNodeLinks)
            .ToImmutableList();

        return result;
    }

    private Task<HttpResponseHandler> _SendPublicNodeGetReqAsync() =>
        HttpRequestBuilder
            .Create("https://uptime.easytier.cn/api/nodes?page=1&per_page=50&is_active=true", HttpMethod.Get)
            .SendAsync();

    #region Information

    /// <summary>
    /// Checks the status of EasyTier network until it is ready or time-out.
    /// </summary>
    /// <returns>Returns 0 when the network is ready, otherwise returns 1 for timeout.</returns>
    public async Task<(bool, EtPlayerList?)> CheckEasyTierStatusAsync()
    {
        var retryCount = 0;

        while (_etProcess is null && retryCount < 10)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            retryCount++;
        }

        if (_etProcess is null)
        {
            return (false, null);
        }

        retryCount = 0;
        while (State is not EtState.Ready && retryCount < 10)
        {
            var info = await _GetPlayersAsync().ConfigureAwait(false);
            if (info.Host is null)
            {
                LogWrapper.Debug("EasyTierEntity", "Retry to get EasyTier Info.");
                await Task.Delay(1000).ConfigureAwait(false);
                retryCount++;
                continue;
            }

            LogWrapper.Debug("EtEntity", "Successfully to get player info from EasyTier CLI.");

            if (info.Host.Ping < 1000)
            {
                State = EtState.Ready;

                return (true, info);
            }

            await Task.Delay(1000).ConfigureAwait(false);
            retryCount++;
        }

        LogWrapper.Debug("EtEntity", "Failed to get player info from EasyTier CLI.");

        return (false, null);
    }

    /// <summary>
    /// Add a port forward to the EasyTier instance.
    /// </summary>
    /// <param name="targetIp">Remote IP</param>
    /// <param name="targetPort">Remote Port</param>
    /// <returns>Forwarded local port</returns>
    public async Task<int> AddPortForwardAsync(string targetIp, int targetPort)
    {
        var localPort = NetworkHelper.NewTcpPort();
        using var cliProcess = new Process();
        cliProcess.StartInfo = new ProcessStartInfo
        {
            FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
            WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
            ErrorDialog = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };
        cliProcess.EnableRaisingEvents = true;
        try
        {
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add tcp 127.0.0.1:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);
            
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add udp 127.0.0.1:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);
            
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add tcp [::]:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);
            
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add udp [::]:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);

            LogWrapper.Debug("ET Cli", await cliProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false) +
                                       await cliProcess.StandardError.ReadToEndAsync().ConfigureAwait(false));
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "ET Cli", "Failed to add port forward.");
        }
        return localPort;
    }

    private async Task _GetCliOutputDebug()
    {
        while (State != EtState.Stopped && Config.Link.EnableCliOutput)
        {
            using var cliProcess = new Process();
            cliProcess.StartInfo = new ProcessStartInfo
            {
                FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
                WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
                Arguments = $"--rpc-portal 127.0.0.1:{_rpcPort} peer",
                ErrorDialog = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        
            try
            {
                cliProcess.Start();
                cliProcess.StandardInput.Close();

                var stdOut = await cliProcess.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
                var stdErr = await cliProcess.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);

                await cliProcess.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                
                var output = stdOut + stdErr;
                
                LogWrapper.Info("EasyTier Cli Debug", "EasyTier Cli 抽样输出: \n" + output);
            }
            catch (Exception e)
            {
                LogWrapper.Error(e, "EasyTier Cli", "Failed to get EasyTier Cli info");
            }

            await Task.Delay(30000);
        }
    }
    
    /// <exception cref="ArgumentException">Thrown if host is duplicated.</exception>
    private async Task<EtPlayerList> _GetPlayersAsync()
    {
        using var cliProcess = new Process();
        cliProcess.StartInfo = new ProcessStartInfo
        {
            FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
            WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
            Arguments = $"--rpc-portal 127.0.0.1:{_rpcPort} -o json peer",
            ErrorDialog = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };
        cliProcess.EnableRaisingEvents = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));

        try
        {
            LogWrapper.Debug("Et Cli", "Trying to get player info.");

            cliProcess.Start();
            cliProcess.StandardInput.Close();

            var stdOut = await cliProcess.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            var stdErr = await cliProcess.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);

            await cliProcess.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var output = stdOut + stdErr;
            //LogWrapper.Debug("ET Cli", output);

            if (JsonNode.Parse(output) is not JsonArray jArray)
            {
                return new EtPlayerList(null, null);
            }


            List<EasyPlayerInfo> players = [];
            EasyPlayerInfo? host = null;
            foreach (var arr in jArray)
            {
                LogWrapper.Debug("Et Cli", "Getting player info.");

                var info = arr.Deserialize<ETPeerInfo>();
                if (info == null)
                {
                    LogWrapper.Debug("Et Cli", "Player info is null.");
                    continue;
                }

                if (info.Hostname.StartsWith("scaffolding-mc-server-", StringComparison.Ordinal))
                {
                    LogWrapper.Debug("Et Cli", $"Find host player: {info.Hostname}");

                    if (host is not null)
                    {
                        LogWrapper.Debug("Et Cli", "Duplicated host player.");
                        throw new ArgumentException("Duplicated host.", nameof(host));
                    }

                    host = _ConvertPeerToPlayer(info);
                    continue;
                }

                LogWrapper.Debug("Et Cli", $"Find player: {info.Hostname}");
                players.Add(_ConvertPeerToPlayer(info));
            }

            LogWrapper.Debug("Et Cli", "Return from GetPlayersAsync().");

            var result = host is null ? players : [host, .. players];

            return new EtPlayerList(result, host);
        }
        catch (TaskCanceledException tce)
        {
            LogWrapper.Error(tce, "EasyTier", "Failed to read CLI output.");
            return new EtPlayerList(null, null);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "EasyTier", "Failed to get EasyTier player list info.");
            return new EtPlayerList(null, null);
        }
    }

    private static EasyPlayerInfo _ConvertPeerToPlayer(ETPeerInfo info)
    {
        var playerInfo = new EasyPlayerInfo
        {
            IsHost = info.Hostname.StartsWith("scaffolding-mc-server", StringComparison.Ordinal),
            HostName = info.Hostname,
            Ip = info.Ipv4,
            Ping = Math.Round(Convert.ToDouble(info.Ping != "-" ? info.Ping : "0")),
            Loss = Math.Round(Convert.ToDouble(info.Loss != "-" ? info.Loss.Replace("%", "") : "0")),
            NatType = info.NatType,
            EasyTierVer = info.ETVersion
        };

        return playerInfo;
    }

    #endregion
}

public record EtPlayerList(IReadOnlyList<EasyPlayerInfo>? Players, EasyPlayerInfo? Host);