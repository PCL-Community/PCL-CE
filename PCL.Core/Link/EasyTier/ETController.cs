using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Net;
using PCL.Core.Utils;
using PCL.Core.Utils.Secret;
using static PCL.Core.Link.EasyTier.ETInfoProvider;
using static PCL.Core.Link.Lobby.LobbyInfoProvider;
using static PCL.Core.Link.Natayark.NatayarkProfileManager;

namespace PCL.Core.Link.EasyTier;
// ReSharper disable InconsistentNaming

public enum ETState
{
    Stopped,
    Running,
    Ready
}

public static class ETController
{
    public static Process? ETProcess { get; private set; }
    public static int ETRpcPort { get; private set; }
    public static ETState Status { get; internal set; }

    public static int Precheck()
    {
        var existedET = Process.GetProcessesByName("easytier-core");
        foreach (var p in existedET)
        {
            LogWrapper.Warn("Link", $"发现已有的 EasyTier 实例，可能影响与启动器所用的实例通信: {p.Id}");
        }

        // 检查文件
        LogWrapper.Info("Link", "EasyTier 路径: " + ETPath);
        if (!(File.Exists(ETPath + "\\easytier-core.exe") && File.Exists(ETPath + "\\easytier-cli.exe") &&
              File.Exists(ETPath + "\\Packet.dll")))
        {
            LogWrapper.Error("Link", "EasyTier 不存在或不完整");
            return 1;
        }

        return 0;
    }

    public static int Launch(bool isHost, string? hostname = null)
    {
        try
        {
            if (TargetLobby == null || Precheck() != 0)
            {
                return 1;
            }

            ETProcess = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo
                {
                    FileName = $"{ETPath}\\easytier-core.exe", WorkingDirectory = ETPath,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            var arguments = new ArgumentsBuilder();

            // 大厅信息
            var name = TargetLobby.NetworkName;
            var secret = TargetLobby.NetworkSecret;

            switch (TargetLobby.Type)
            {
                case LobbyType.PCLCE:
                    name = ETNetworkNamePrefix + name;
                    secret = ETNetworkSecretPrefix + secret;
                    break;
                case LobbyType.Terracotta:
                    name = "terracotta-mc-" + name;
                    break;
                default:
                    throw new NotSupportedException("不支持的大厅类型: " + TargetLobby.Type);
            }

            arguments.AddFlag("no-tun");
            arguments.Add("network-name", name);
            arguments.Add("network-secret", secret);
            arguments.Add("relay-network-whitelist", name);
            arguments.Add("private-mode", "true");
            // 网络参数
            if (isHost)
            {
                LogWrapper.Info("Link", $"本机作为创建者创建大厅，EasyTier 网络名称: {name}");
                arguments.Add("i", "10.114.51.41");
                arguments.Add("tcp-whitelist", TargetLobby.Port.ToString());
                arguments.Add("udp-whitelist", TargetLobby.Port.ToString());
            }
            else
            {
                LogWrapper.Info("Link", $"本机作为加入者加入大厅，EasyTier 网络名称: {name}");
                arguments.AddFlag("d");
                arguments.Add("tcp-whitelist", "0");
                arguments.Add("udp-whitelist", "0");

                JoinerLocalPort = NetworkHelper.NewTcpPort();
                LogWrapper.Info("Link", $"ET 端口转发: 远程 {TargetLobby.Port} -> 本地 {JoinerLocalPort}");
                arguments.Add("port-forward", $"tcp://127.0.0.1:{JoinerLocalPort}/{TargetLobby.Ip}:{TargetLobby.Port}");
                arguments.Add("port-forward", $"udp://127.0.0.1:{JoinerLocalPort}/{TargetLobby.Ip}:{TargetLobby.Port}");
            }

            // 节点设置
            var relays = ETRelay.RelayList;
            var customNodes = Config.Link.RelayServer;
            foreach (var node in customNodes.Split([';'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (node.Contains("tcp://") || node.Contains("udp://"))
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
                    LogWrapper.Warn("Link", $"无效的自定义节点 URL: {node}");
                }
            }

            foreach (var relay in
                     from relay in relays
                     let serverType = Config.Link.ServerType
                     where (relay.Type == ETRelayType.Selfhosted && serverType != 2) ||
                           (relay.Type == ETRelayType.Community && serverType == 1) || relay.Type == ETRelayType.Custom
                     select relay)
            {
                arguments.Add("p", relay.Url);
            }

            // 中继行为设置
            if (Config.Link.RelayType == 1)
            {
                arguments.AddFlag("disable-p2p");
            }

            // 数据流代理设置
            arguments.AddFlag("enable-quic-proxy");
            arguments.AddFlag("enable-kcp-proxy");
            arguments.AddFlag("use-smoltcp");
            arguments.Add("encryption-algorithm", "chacha20");
            arguments.Add("default-protocol", Config.Link.ProtocolPreference.ToString().ToLower());
            arguments.AddFlagIf(!Config.Link.TryPunchSym, "disable-sym-hole-punching");
            arguments.AddFlagIf(!Config.Link.EnableIPv6, "disable-ipv6");

            // 用户名与其他参数
            arguments.AddFlagIf(Config.Link.LatencyFirstMode, "latency-first");
            arguments.Add("compression", "zstd");
            arguments.AddFlag("multi-thread");
            arguments.Add("machine-id", Identify.LauncherId);

            // TODO: 等待玩家档案迁移以获取正在使用的档案名称
            var showName = "default";
            if (AllowCustomName && !string.IsNullOrWhiteSpace(Config.Link.Username))
            {
                showName = Config.Link.Username;
            }
            else if (!string.IsNullOrWhiteSpace(NaidProfile.Username))
            {
                showName = NaidProfile.Username;
            }

            arguments.Add("hostname",
                (isHost ? "H|" : "J|") + showName + (!string.IsNullOrWhiteSpace(hostname) ? "|" + hostname : ""));

            // 指定 RPC 端口以避免与其他 ET 实例冲突
            ETRpcPort = NetworkHelper.NewTcpPort();
            arguments.Add("rpc-portal", $"127.0.0.1:{ETRpcPort}");

            // 启动
            ETProcess.StartInfo.Arguments = arguments.GetResult();
            LogWrapper.Info("Link", "启动 EasyTier");
            // 操作 UI 显示大厅编号（可能写到 XAML 下面 UI 控制那部分去？）
            ETProcess.Start();
            Status = ETState.Running;
            return 0;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "尝试启动 EasyTier 时遇到问题");
            Status = ETState.Stopped;
            ETProcess = null;
            return 1;
        }
    }

    public static void Exit()
    {
        if (Status == ETState.Stopped || ETProcess == null) return;
        try
        {
            LogWrapper.Info("Link", $"关闭 EasyTier (PID: {ETProcess.Id})");
            ETProcess.Kill();
            ETProcess.WaitForExit(200);
        }
        catch (InvalidOperationException)
        {
            LogWrapper.Warn("Link", "EasyTier 进程不存在，可能已退出");
        }
        catch (NullReferenceException)
        {
            LogWrapper.Warn("Link", "EasyTier 进程不存在，可能已退出");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "关闭 EasyTier 时遇到问题");
        }
        finally
        {
            Status = ETState.Stopped;
            ETProcess = null;
        }
    }
}
