using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FluentValidation;
using fNbt;
using PCL.Core.Link.McPing;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Minecraft;
using PCL.Core.Utils.Validate;

namespace PCL;

public partial class PageInstanceServer : MyPageRight
{
    private const int DebounceInterval = 2000;

    public static readonly List<MinecraftServerInfo> ServerList = new();
    private static readonly List<ServerCard> ServerCardList = new();

    private CancellationTokenSource _cts;

    private DateTime _lastRefresh = DateTime.MinValue;

    public PageInstanceServer()
    {
        InitializeComponent();
        Loaded += PageLoaded;
        IsVisibleChanged += PageInstanceServer_IsVisibleChanged;
    }

    private async void PageLoaded(object e, RoutedEventArgs sender)
    {
        ServerList.Clear();
        ServerCardList.Clear();
        PanServers.Children.Clear();

        await LoadServersFromFile();
        RefreshTip();

        foreach (var server in ServerList)
        {
            var serverCard = new ServerCard();
            serverCard.RemoveServer += RemoveServerEvent;
            serverCard.EditServer += (a, b) => this.EditServer(a, (ServerCard.ResultEventArgs)b);
            serverCard.UpdateServerInfo(server);
            ServerCardList.Add(serverCard);
            PanServers.Children.Add(serverCard);
        }

        PingAllServers();
    }

    private void PageInstanceServer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
            if (_cts is not null)
            {
                _cts.Cancel();
                _cts.Dispose(); // 清理旧的 CancellationTokenSource
                _cts = null;
            }
    }

    private async void RemoveServerEvent(object sender, EventArgs e)
    {
        // Get server index
        var index = PanServers.Children.IndexOf((UIElement)sender);
        if (index < 0)
        {
            ModMain.Hint("无法找到服务器在列表中的索引", ModMain.HintType.Critical);
            return;
        }

        // Read NBT file
        var nbtData =
            await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(
                Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat"), "servers");
        if (nbtData is null)
        {
            ModMain.Hint("无法读取服务器数据文件", ModMain.HintType.Critical);
            return;
        }

        // Remove server from NBT data
        nbtData.RemoveAt(index);
        var clonedNbtData = (NbtList)nbtData.Clone();

        // Write back to NBT file
        if (!await NbtFileHandler.WriteTagInNbtFileAsync(clonedNbtData,
                Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat")))
        {
            ModMain.Hint("无法写入服务器数据文件", ModMain.HintType.Critical);
            return;
        }

        // Remove server from list and UI
        ServerList.RemoveAt(index);
        ServerCardList.Remove((ServerCard)sender);
        if (ServerList.Count == 0) RefreshTip();

        // Remove UI element
        PanServers.Children.Remove((UIElement)sender);

        // Success message
        ModMain.Hint("服务器已移除", ModMain.HintType.Finish);
    }

    private async void EditServer(object sender, ServerCard.ResultEventArgs e)
    {
        // Read NBT file
        var nbtData =
            await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat"),
                "servers");
        if (nbtData is null)
        {
            ModMain.Hint("无法读取服务器数据文件", ModMain.HintType.Critical);
            return;
        }

        // Get server index
        var index = PanServers.Children.IndexOf((UIElement)sender);
        if (index < 0 || index >= nbtData.Count)
        {
            ModMain.Hint("无法找到服务器在列表中的索引", ModMain.HintType.Critical);
            return;
        }

        // Verify server data
        var server = nbtData[index] as NbtCompound;

        // Update server data
        server["name"] = new NbtString("name", e.Param1);
        server["ip"] = new NbtString("ip", e.Param2);

        // Write updated NBT data
        var clonedNbtData = (NbtList)nbtData.Clone();
        if (!await NbtFileHandler.WriteTagInNbtFileAsync(clonedNbtData,
                Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat")))
        {
            ModMain.Hint("无法写入服务器数据文件", ModMain.HintType.Critical);
            return;
        }

        var serverCard = sender as ServerCard;

        serverCard.Server.Name = e.Param1;
        serverCard.Server.Address = e.Param2;

        await serverCard.RefreshServerStatus(true);

        // Success message
        ModMain.Hint("服务器信息已更新", ModMain.HintType.Finish);
    }

    /// <summary>
    ///     刷新服务器列表
    /// </summary>
    public async void RefreshServers()
    {
        ModBase.Log("刷新服务器列表");
        try
        {
            // 读取服务器信息
            await LoadServersFromFile();

            // 在UI线程中更新界面
            ModBase.RunInUi(() => UpdateServerUi());

            // 异步ping所有服务器
            PingAllServers();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新服务器列表失败", ModBase.LogLevel.Feedback);
            ModBase.RunInUi(() => ModMain.Hint("刷新服务器列表失败：" + ex.Message, ModMain.HintType.Critical));
        }
    }

    private void BtnRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds < DebounceInterval)
        {
            ModMain.Hint("请勿频繁刷新！");
            return;
        }

        _lastRefresh = DateTime.Now;
        ModMain.Hint("正在刷新服务器列表，请稍候...");
        try
        {
            RefreshServers();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新服务器列表失败", ModBase.LogLevel.Feedback);
            ModMain.Hint("刷新服务器列表失败：" + ex.Message, ModMain.HintType.Critical);
        }
    }

    private async void BtnAddServer_Click(object sender, MouseButtonEventArgs e)
    {
        var result = GetServerInfo(new MinecraftServerInfo { Name = "Minecraft服务器", Address = "" });
        if (result.Success)
        {
            var newServer = new MinecraftServerInfo
            {
                Name = result.Name,
                Address = result.Address,
                Status = ServerStatus.Unknown
            };
            ServerList.Add(newServer);

            RefreshTip();

            var serverCard = new ServerCard();
            serverCard.RemoveServer += RemoveServerEvent;
            serverCard.EditServer += (a, b) => this.EditServer(a, (ServerCard.ResultEventArgs)b);
            serverCard.UpdateServerInfo(newServer);
            ServerCardList.Add(serverCard);
            PanServers.Children.Add(serverCard);

            await serverCard.RefreshServerStatus(false);

            var serversDatPath = Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat");

            NbtList nbtData;
            if (!File.Exists(serversDatPath))
            {
                nbtData = new NbtList("servers", NbtTagType.Compound);
                RefreshTip();
            }
            else
            {
                nbtData = await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(serversDatPath, "servers");
            }

            if (nbtData is not null)
            {
                var server = new NbtCompound();
                server["name"] = new NbtString("name", result.Name);
                server["ip"] = new NbtString("ip", result.Address);
                if (nbtData.Count == 0) nbtData.ListType = NbtTagType.Compound;
                nbtData.Add(server);
                var clonedNbtData = (NbtList)nbtData.Clone();
                await NbtFileHandler.WriteTagInNbtFileAsync(clonedNbtData, serversDatPath);
            }
        }
    }

    public static (string Name, string Address, bool Success) GetServerInfo(MinecraftServerInfo server)
    {
        var newName = ModMain.MyMsgBoxInput("编辑服务器信息", "请输入新的服务器名称：", server.Name,
            [new NullOrWhiteSpaceValidator()]);

        if (string.IsNullOrEmpty(newName)) return (string.Empty, string.Empty, false);

        var newAddress = ModMain.MyMsgBoxInput("编辑服务器信息", "请输入新的服务器地址：", server.Address,
            [new NullOrWhiteSpaceValidator()]);
        if (string.IsNullOrEmpty(newAddress)) return (string.Empty, string.Empty, false);
        return (newName, newAddress, true);
    }

    /// <summary>
    ///     从servers.dat文件读取服务器信息
    /// </summary>
    private async Task LoadServersFromFile()
    {
        ServerList.Clear();

        var serversFile = Path.Combine(PageInstanceLeft.Instance.PathIndie, "servers.dat");
        if (!File.Exists(serversFile))
            return;

        try
        {
            // 读取NBT格式的servers.dat文件
            var nbtData = await NbtFileHandler.ReadTagInNbtFileAsync<NbtList>(serversFile, "servers");
            ParseServersFromNBT(nbtData);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取servers.dat文件失败");
        }
    }

    /// <summary>
    ///     解析NBT格式的服务器数据
    /// </summary>
    private void ParseServersFromNBT(NbtList serversList)
    {
        if (serversList is not null)
        {
            ModBase.Log($"Found {serversList.Count} servers:");

            // 遍历 servers 列表中的每个服务器
            for (int i = 0, loopTo = serversList.Count - 1; i <= loopTo; i++)
            {
                var server = serversList[i] as NbtCompound;
                if (server is not null)
                {
                    // 提取服务器信息
                    // Dim hidden As Byte = If(server.Get(Of NbtByte)("hidden")?.Value, 0)
                    var ip = server.Get<NbtString>("ip")?.Value ?? "Unknown";
                    var name = server.Get<NbtString>("name")?.Value ?? "Unknown";
                    var iconBase64 = server.Get<NbtString>("icon")?.Value;

                    ModBase.Log($"服务器 {i + 1}:");
                    ModBase.Log($"  名字: {name}");
                    ModBase.Log($"  IP: {ip}");
                    // Log($"  Hidden: {If(hidden = 1, "Yes", "No")}")
                    ServerList.Add(new MinecraftServerInfo
                    {
                        Name = name,
                        Address = ip,
                        Status = ServerStatus.Unknown,
                        Icon = iconBase64
                    });
                }
            }
        }
        else
        {
            ModBase.Log("No 'servers' list found in servers.dat.");
        }
    }

    /// <summary>
    ///     更新服务器UI显示
    /// </summary>
    private void UpdateServerUi()
    {
        PanServers.Children.Clear();

        RefreshTip();

        foreach (var server in ServerList)
        {
            var serverCard = new ServerCard();
            serverCard.RemoveServer += RemoveServerEvent;
            serverCard.EditServer += (a, b) => this.EditServer(a, (ServerCard.ResultEventArgs)b);
            serverCard.UpdateServerInfo(server);
            ServerCardList.Add(serverCard);
            PanServers.Children.Add(serverCard);
        }
    }

    private void RefreshTip()
    {
        if (ServerList.Count == 0)
        {
            ModBase.Log("没有找到任何服务器");
            PanNoServer.Visibility = Visibility.Visible;
            PanContent.Visibility = Visibility.Collapsed;
            PanServers.Visibility = Visibility.Collapsed;
            return;
        }

        ModBase.Log("找到服务器列表");
        PanNoServer.Visibility = Visibility.Collapsed;
        PanContent.Visibility = Visibility.Visible;
        PanServers.Visibility = Visibility.Visible;
    }

    private async void PingAllServers()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var semaphore = new SemaphoreSlim(5); // 限制最多 5 个并发任务

        var tasks = new List<Task>();
        try
        {
            var snapshot = ServerCardList.ToList();
            foreach (var server in snapshot)
            {
                var currentServer = server;
                await semaphore.WaitAsync(token);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await currentServer.RefreshServerStatus(false, token);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, $"Ping 服务器失败: {currentServer}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            await Task.WhenAll(tasks); // 等待所有任务完成
        }
        catch (OperationCanceledException ex)
        {
            ModBase.Log("PingAllServers 被取消", ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "PingAllServers 失败");
        }
    }

    /// <summary>
    ///     ping单个服务器
    /// </summary>
    public static async Task<MinecraftServerInfo> PingServer(MinecraftServerInfo server, CancellationToken token)
    {
        try
        {
            var addr = await ServerAddressResolver.GetResolvedServerAddressAsync(server.Address, token);
            using (var query = McPingServiceFactory.CreateService(addr.Host, addr.Ip, addr.Port))
            {
                McPingResult? result;
                ModBase.Log("Pinging server: " + server.Address + ":" + addr.Port);
                result = await query.PingAsync(token); // 传递 token
                ModBase.Log("Ping result: " + (result is not null ? "Success" : "Failed"));
                if (result is not null)
                {
                    server.Status = ServerStatus.Online;
                    server.PlayerCount = result.Players.Online;
                    server.MaxPlayers = result.Players.Max;
                    server.Description = result.Description;
                    server.Version = result.Version.Name;
                    server.Ping = (int)result.Latency;
                    server.Icon = result.Favicon;
                }
                else
                {
                    server.Status = ServerStatus.Offline;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            server.Status = ServerStatus.Offline;
            ModBase.Log("Ping 服务器被取消: " + server.Address, ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            server.Status = ServerStatus.Offline;
            ModBase.Log(ex, $"Ping 服务器失败: {server.Address}:{server.Port}");
        }

        return server;
    }
}

/// <summary>
///     Minecraft服务器信息类
/// </summary>
public class MinecraftServerInfo
{
    public string Name { get; set; }
    public string Address { get; set; }
    public int Port { get; set; } = 25565;
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public int Ping { get; set; }
    public string Icon { get; set; } = "";
}

/// <summary>
///     服务器状态枚举
/// </summary>
public enum ServerStatus
{
    Unknown,
    Online,
    Offline,
    Pinging
}