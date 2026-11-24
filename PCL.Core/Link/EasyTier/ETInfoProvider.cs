using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Link.EasyTier;

// ReSharper disable InconsistentNaming, CompareOfFloatsByEqualityOperator
public enum ETConnectionType
{
    Local,
    P2P,
    Relay,
    Unknown
}

public class ETPlayerInfo
{
    public required bool IsHost { get; init; }

    /// <summary>
    ///     EasyTier 设置的原始主机名
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    ///     显示的用户名，依次可能为自定义用户名、NAID 用户名
    /// </summary>
    public string? Username { get; init; }

    public string? McName { get; init; }

    /// <summary>
    ///     连接方式
    /// </summary>
    public ETConnectionType Cost { get; init; } = ETConnectionType.Unknown;

    /// <summary>
    ///     延迟 (ms)
    /// </summary>
    public double Ping { get; set; }

    /// <summary>
    ///     丢包率 (%)
    /// </summary>
    public double Loss { get; init; }

    public string? NatType { get; init; }

    /// <summary>
    ///     节点的 EasyTier 版本
    /// </summary>
    public string? ETVersion { get; init; }
}

public static class ETInfoProvider
{
    public const string ETNetworkNamePrefix = "PCLCELobby";
    public const string ETNetworkSecretPrefix = "PCLCEETLOBBY2025";
    public const string ETVersion = EasyTierMetadata.CurrentEasyTierVer;

    private static ETConnectionType _GetConnectionType(string cost)
    {
        if (IsContains("p2p")) return ETConnectionType.P2P;
        if (IsContains("relay")) return ETConnectionType.Relay;
        if (IsContains("local")) return ETConnectionType.Local;
        return ETConnectionType.Unknown;

        bool IsContains(string str)
        {
            return cost.Contains(str, StringComparison.InvariantCultureIgnoreCase);
        }
    }

    /// <summary>
    ///     检查 EasyTier 状态，若状态正常则返回 0。
    /// </summary>
    /// <returns></returns>
    public static async Task<int> CheckETStatusAsync()
    {
        var retryCount = 0;
        var process = ETController.ETProcess;
        while (process is null && retryCount < 10)
        {
            await Task.Delay(1000);
            retryCount++;
        }

        if (process is not null)
        {
            while (ETController.Status != ETState.Ready)
            {
                var info = (await GetPlayerListAsync().ConfigureAwait(false)).Item1?[0];
                if (info is null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                if (info.Ping != 1000) ETController.Status = ETState.Ready;
                await Task.Delay(1000);
            }

            return 0;
        }

        return 1;
    }

    /// <summary>
    ///     获取 EasyTier 网络的成员列表和本地信息，若获取失败则均返回 null。
    /// </summary>
    /// <returns>Tuple(玩家列表, 本地信息)</returns>
    public static async Task<Tuple<List<ETPlayerInfo>?, ETPlayerInfo?>> GetPlayerListAsync()
    {
        try
        {
            var result = await EasyTierCli
                .RunAsync(
                    $"--rpc-portal 127.0.0.1:{ETController.ETRpcPort} -o json peer",
                    TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);

            var output = result.Output;
            if (JsonCompat.ParseNode(output) is not JsonArray json)
                return new Tuple<List<ETPlayerInfo>?, ETPlayerInfo?>(null, null);

            var playerList = new List<ETPlayerInfo>();
            ETPlayerInfo? localInfo = null;
            foreach (var p in json)
            {
                var info = p.Deserialize<ETPeerInfo>(JsonCompat.SerializerOptions);
                if (info is null) continue;
                if (info.Hostname.StartsWith("PublicServer")) continue; // 服务器

                var hostnameSplit = info.Hostname.Split('|');
                var playerInfo = new ETPlayerInfo
                {
                    IsHost = info.Hostname.StartsWith("H|") || info.Ipv4 == "10.144.144.1",
                    Hostname = info.Hostname,
                    Username = hostnameSplit.Length >= 2 ? hostnameSplit[1] : null,
                    McName = hostnameSplit.Length == 3 ? hostnameSplit[2] : null,
                    Cost = _GetConnectionType(info.Cost),
                    Ping = Math.Round(Convert.ToDouble(info.Ping != "-" ? info.Ping : "0")),
                    Loss = Math.Round(
                        Convert.ToDouble(info.Loss != "-" ? info.Loss.Replace("%", "") : "0")),
                    NatType = info.NatType,
                    ETVersion = info.ETVersion
                };

                if (playerInfo.IsHost)
                    playerList.Insert(0, playerInfo); // 主机信息放在列表首位
                else
                    playerList.Add(playerInfo);
                if (playerInfo.Cost == ETConnectionType.Local) localInfo = playerInfo;
            }

            return new Tuple<List<ETPlayerInfo>?, ETPlayerInfo?>(playerList, localInfo);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "获取 EasyTier 网络成员列表失败");
            return new Tuple<List<ETPlayerInfo>?, ETPlayerInfo?>(null, null);
        }
    }
}