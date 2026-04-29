using System.IO;
using System.Windows;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Network;

namespace PCL;

public partial class PageLaunchRight : IRefreshable
{
    public PageLaunchRight()
    {
        InitializeComponent();
        OnlineLoader = new ModLoader.LoaderTask<string, int>("下载主页", OnlineLoaderSub)
            { ReloadTimeout = 10 * 60 * 1000 };
        Loaded += (_, _) => Init();
        Loaded += (_, _) => Refresh();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        PanLog.Visibility = ModBase.ModeDebug ? Visibility.Visible : Visibility.Collapsed;
        // 社区版提示
        PanHint.Visibility = States.Hint.CEMessage
            ? Visibility.Visible
            : Visibility.Collapsed;
        LabHint1.Text =
            $"你正在使用 PCL 社区版！此版本为独立开发和维护，与官方版本维护路线不同，体验有所出入。{"\r\n"}{"\r\n"}如果你是意外下载到了社区版，我们十分建议您下载 PCL 官方版长期使用，此发行版本对新手用户体验可能不友好。{"\r\n"}此外，社区版的问题请向社区版的仓库提交 Issue，不要向官方仓库反馈社区版的问题哦！{"\r\n"}";
        LabHint2.Text = "若要永久隐藏此提示，请输入正确的 PCL CE 开发组织名称。";
    }

    // 暂时关闭快照版提示
    private void BtnHintClose_Click(object sender, EventArgs e)
    {
        var input = ModMain.MyMsgBoxInput("输入 PCL CE 开发组织名称");
        if (string.IsNullOrWhiteSpace(input))
            return;
        input = new string(input.Where(x => char.IsAsciiLetter(x)).ToArray()).ToLower();
        if (input.Contains("pclcommunity"))
        {
            ModAnimation.AniDispose(PanHint, true);
            States.Hint.CEMessage = false;
        }
        else
        {
            ModMain.Hint("不太对哦……");
        }
    }

    #region 主页

    /// <summary>
    ///     刷新主页。
    /// </summary>
    private void Refresh()
    {
        ModBase.RunInNewThread(() =>
            {
                try
                {
                    lock (RefreshLock)
                    {
                        RefreshReal();
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "加载 PCL 主页自定义信息失败",
                        ModBase.ModeDebug ? ModBase.LogLevel.Msgbox : ModBase.LogLevel.Hint);
                }
            }, $"刷新主页 #{ModBase.GetUuid()}");
    }

    private void RefreshReal()
    {
        var content = "";
        string url = null;

        var uiCustomType = (int)Config.Preference.Homepage.Type;

        if (uiCustomType == 1)
        {
            // 本地文件
            LogWrapper.Info("[Page] 主页自定义数据来源：本地文件");
            content = ModBase.ReadFile(Path.Combine(ModBase.ExePath, "PCL", "Custom.xaml"));
        }
        else if (uiCustomType == 2)
        {
            // 网络文件
            url = (string)Config.Preference.Homepage.CustomUrl;
            content = LoadFromNetwork(url);
        }
        else if (uiCustomType == 3)
        {
            // 预设主页
            var preset = (int)Config.Preference.Homepage.SelectedPreset;
            switch (preset)
            {
                case 0:
                    LogWrapper.Info("[Page] 主页预设：你知道吗");
                    var hintText = GetRandomHint();
                    content = $@"
    <local:MyCard Title=""你知道吗？"" Margin=""0,0,0,15"">
        <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{hintText}"" TextWrapping=""Wrap"" Foreground=""{{DynamicResource ColorBrush1}}"" />
        <local:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right"" 
            EventType=""刷新主页"" EventData=""/""
            Logo=""M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z"" />
    </local:MyCard>";
                    break;

                case 1:
                    LogWrapper.Info("[Page] 主页预设：回声洞 已被移除");
                    ModMain.MyMsgBox("回声洞 因为只有空壳因此已被移除，请前往设置选择其他预设主页");
                    return;

                case 2:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 新闻");
                    url = "https://pcl.mcnews.thestack.top";
                    content = LoadFromNetwork(url);
                    break;

                case 3:
                    LogWrapper.Info("[Page] 主页预设：简单主页");
                    url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/MFn233/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 4:
                    LogWrapper.Info("[Page] 主页预设：每日整合包推荐");
                    url = "https://pclsub.sodamc.com/";
                    content = LoadFromNetwork(url);
                    break;

                case 5:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 皮肤推荐");
                    url = "https://forgepixel.com/pcl_sub_file";
                    content = LoadFromNetwork(url);
                    break;

                case 6:
                    LogWrapper.Info("[Page] 主页预设：OpenBMCLAPI 仪表盘 Lite");
                    url = "https://pcl-bmcl.milu.ink/";
                    content = LoadFromNetwork(url);
                    break;

                case 7:
                    LogWrapper.Info("[Page] 主页预设：主页市场");
                    url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 8:
                    LogWrapper.Info("[Page] 主页预设：更新日志");
                    url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Joker2184/UpdateHomepage.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 9:
                    LogWrapper.Info("[Page] 主页预设：PCL 新功能说明书");
                    url = "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 10:
                    LogWrapper.Info("[Page] 主页预设：OpenMCIM Dashboard");
                    url = "https://files.mcimirror.top/PCL";
                    content = LoadFromNetwork(url);
                    break;

                case 11:
                    LogWrapper.Info("[Page] 主页预设：杂志主页");
                    url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 12:
                    LogWrapper.Info("[Page] 主页预设：PCL GitHub 仪表盘");
                    url = "https://ddf.pcl-community.org/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 13:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 更新摘要");
                    url = "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 14:
                    LogWrapper.Info("[Page] 主页预设：PCL CE 公告栏");
                    url = "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml";
                    content = LoadFromNetwork(url);
                    break;
                case 15:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 信息流");
                    Dispatcher.Invoke(() =>
                    {
                        if (ModMain.FrmHomepageNews == null) 
                            ModMain.FrmHomepageNews = new PageHomepageNewsView();
                        PanCustom.Children.Clear();
                        PanCustom.Children.Add(ModMain.FrmHomepageNews);
                    });
                    return;
            }
        }

        ModBase.RunInUi(() => LoadContent(content));
    }

    /// <summary>
    ///     根据 URL 加载网络内容，优先使用缓存
    /// </summary>
    private string LoadFromNetwork(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        var cachePath = Path.Combine(ModBase.PathTemp, "Cache", "Custom.xaml");
        var cachedUrl = (string)States.UI.SavedHomepageUrl;

        if (url == cachedUrl && File.Exists(cachePath))
        {
            LogWrapper.Info("[Page] 主页自定义数据来源：联网缓存文件");
            // 后台更新缓存
            OnlineLoader.Start(url);
            return ModBase.ReadFile(cachePath);
        }

        LogWrapper.Info("[Page] 主页自定义数据来源：联网全新下载");
        HintWrapper.Show("正在加载主页……");
        ModBase.RunInUiWait(() => LoadContent("")); // 先清空页面
        States.UI.SavedHomepageVersion = "";
        OnlineLoader.Start(url); // 下载完成后将会再次触发更新
        return "";
    }

    private readonly object RefreshLock = new();

    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        string[] lines = null;

        // 外部文件
        var externalPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\PCL\\hints.txt";
        if (File.Exists(externalPath))
        {
            try
            {
                lines = File.ReadAllLines(externalPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
            }
            catch
            {
                ModBase.Log($"[Page] 读取外部文件失败：{externalPath}", ModBase.LogLevel.Hint);
            }
        }

        // 嵌入式资源
        if (lines == null || lines.Length == 0)
        {
            using (var reader = new StreamReader(Application.GetResourceStream(new Uri("pack://application:,,,/Plain Craft Launcher 2;component/Resources/hints.txt", UriKind.Absolute)).Stream))
            {
                lines = reader.ReadToEnd()
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
            }
        }

        // 长度限制
        if (enableLengthLimit)
        {
            var shortLines = lines.Where(l => l.Length < 50).ToArray();
            if (shortLines.Length > 0) lines = shortLines;
        }

        // 随机返回
        var hint = lines[Random.Shared.Next(lines.Length)];
        return raw ? hint : hint.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    // 联网获取主页文件
    private readonly ModLoader.LoaderTask<string, int> OnlineLoader;

    private void OnlineLoaderSub(ModLoader.LoaderTask<string, int> Task)
    {
        var Address = Task.Input; // #3721 中连续触发两次导致内容变化
        try
        {
            // 获取版本校验地址
            string VersionAddress;
            if (Address.Contains(".xaml"))
            {
                VersionAddress = Address.Replace(".xaml", ".xaml.ini");
            }
            else
            {
                VersionAddress = Address.BeforeFirst("?");
                if (!VersionAddress.EndsWith("/"))
                    VersionAddress += "/";
                VersionAddress += "version";
                if (Address.Contains("?"))
                    VersionAddress += "?" + Address.AfterFirst("?");
            }

            // 校验版本
            var Version = "";
            var NeedDownload = true;
            try
            {
                Version = Requester.FetchString(VersionAddress);
                if (Version.Length > 1000)
                    throw new Exception($"获取的主页版本过长（{Version.Length} 字符）");
                var CurrentVersion = States.UI.SavedHomepageVersion;
                if (!string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(CurrentVersion) &&
                    (Version ?? "") == (CurrentVersion ?? ""))
                {
                    ModBase.Log($"[Page] 当前缓存的主页已为最新，当前版本：{Version}，检查源：{VersionAddress}");
                    NeedDownload = false;
                }
                else
                {
                    ModBase.Log($"[Page] 需要下载联网主页，当前版本：{Version}，检查源：{VersionAddress}");
                }
            }
            catch (Exception exx)
            {
                ModBase.Log(exx, "联网获取主页版本失败", ModBase.LogLevel.Developer);
                ModBase.Log($"[Page] 无法检查联网主页版本，将直接下载，检查源：{VersionAddress}");
            }

            // 实际下载
            if (NeedDownload)
            {
                var FileContent = Requester.FetchString(Address);
                ModBase.Log($"[Page] 已联网下载主页，内容长度：{FileContent.Length}，来源：{Address}");
                States.UI.SavedHomepageUrl = Address;
                States.UI.SavedHomepageVersion = Version;
                ModBase.WriteFile(ModBase.PathTemp + @"Cache\Custom.xaml", FileContent);
            }

            // 要求刷新
            ModBase.RunInUi(Refresh); // 不直接调用 Refresh，以防止死循环（#6245）
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"下载主页失败（{Address}）", ModBase.ModeDebug ? ModBase.LogLevel.Msgbox : ModBase.LogLevel.Hint);
        }
    }

    /// <summary>
    ///     立即强制刷新主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    public void ForceRefresh()
    {
        ModBase.Log("[Page] 要求强制刷新主页");
        ClearCache();
        // 实际的刷新
        if (ModMain.FrmMain.PageCurrent.Page == FormMain.PageType.Launch)
        {
            PanBack.ScrollToHome();
            Refresh();
        }
        else
        {
            ModMain.FrmMain.PageChange(FormMain.PageType.Launch);
        }
    }

    void IRefreshable.Refresh()
    {
        ForceRefresh();
    }

    /// <summary>
    ///     清空主页缓存信息。
    /// </summary>
    private void ClearCache()
    {
        LoadedContentHash = -1;
        OnlineLoader.Input = "";
        States.UI.SavedHomepageUrl = "";
        States.UI.SavedHomepageVersion = "";
        ModBase.Log("[Page] 已清空主页缓存");
    }

    /// <summary>
    ///     从文本内容中加载主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    private void LoadContent(string Content)
    {
        lock (LoadContentLock)
        {
            // 如果加载目标内容一致则不加载
            var Hash = Content.GetHashCode();
            if (Hash == LoadedContentHash)
                return;
            LoadedContentHash = Hash;
            // 实际加载内容
            PanCustom.Children.Clear();
            if (string.IsNullOrWhiteSpace(Content))
            {
                ModBase.Log("[Page] 实例化：清空主页 UI，来源为空");
                return;
            }

            var LoadStartTime = DateTime.Now;
            try
            {
                // 修改时应同时修改 PageOtherHelpDetail.Init
                Content = ModMain.ArgumentReplace(Content);
                while (Content.Contains("xmlns"))
                    Content = Content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");
                Content =
                    $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{Content}</StackPanel>";
                ModBase.Log($"[Page] 实例化：加载主页 UI 开始，最终内容长度：{Content.Count()}");
                PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(Content));
            }
            catch (Exception ex)
            {
                if (ModBase.ModeDebug)
                {
                    ModBase.Log(ex, $"加载失败的主页内容：\r\n{Content}");
                    if (ModMain.MyMsgBox(
                            ex is UnauthorizedAccessException
                                ? ex.Message
                                : $"主页内容编写有误，请根据下列错误信息进行检查：\r\n{ex}", "加载主页界面失败", "重试", "取消") ==
                        1) goto Refresh; // 防止 SyncLock 死锁
                }
                else
                {
                    ModBase.Log(ex, "加载主页界面失败", ModBase.LogLevel.Hint);
                }

                return;
            }

            var LoadCostTime = (DateTime.Now - LoadStartTime).Milliseconds;
            ModBase.Log($"[Page] 实例化：加载主页 UI 完成，耗时 {LoadCostTime}ms");
            if (LoadCostTime > 3000)
                ModMain.Hint($"主页加载过于缓慢（花费了 {Math.Round(LoadCostTime / 1000d, 1)} 秒），请向主页作者反馈此问题，或暂时停止使用该主页");
        }

        return;
        Refresh: ;

        ForceRefresh();
    }

    private int LoadedContentHash = -1;
    private readonly object LoadContentLock = new();

    #endregion
}
