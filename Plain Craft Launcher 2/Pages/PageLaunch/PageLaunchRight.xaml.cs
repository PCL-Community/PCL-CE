using System.IO;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageLaunchRight : IRefreshable
{
    public PageLaunchRight()
    {
        InitializeComponent();
        onlineLoader = new ModLoader.LoaderTask<string, int>(
            Lang.Text("Launch.Homepage.Task.Download"),
            OnlineLoaderSub)
        {
            reloadTimeout = 10 * 60 * 1000
        };
        Loaded += (_, _) => Init();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => _DisposeHomepageLiveWatcher();
    }

    private void Init()
    {
        PanBack.ScrollToHome();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        PanLog.Visibility = LauncherRuntime.ModeDebug ? Visibility.Visible : Visibility.Collapsed;
        // 社区版提示
        PanHint.Visibility = States.Hint.CEMessage
            ? Visibility.Visible
            : Visibility.Collapsed;
        LabHint1.Text = Lang.Text("Launch.Right.CommunityHint.Message");
        LabHint2.Text = Lang.Text("Launch.Right.CommunityHint.HidePrompt");
        _EnsureHomepageLiveWatcher();
    }

    // 暂时关闭快照版提示
    private void BtnHintClose_Click(object sender, EventArgs e)
    {
        var input = ModMain.MyMsgBoxInput(Lang.Text("Launch.Right.CommunityHint.InputTitle"));
        if (string.IsNullOrWhiteSpace(input))
            return;
        input = new string(input.Where(char.IsAsciiLetter).ToArray()).ToLower();
        if (input.Contains("pclcommunity"))
        {
            ModAnimation.AniDispose(PanHint, true);
            States.Hint.CEMessage = false;
        }
        else
        {
            HintService.Hint(Lang.Text("Launch.Right.CommunityHint.WrongInput"));
        }
    }

    #region 主页

    /// <summary>
    ///     刷新主页。
    /// </summary>
    private void Refresh()
    {
        PCL.Core.App.Basics.RunInNewThread(() =>
            {
                try
                {
                    lock (refreshLock)
                    {
                        RefreshReal();
                    }
                }
                catch (Exception ex)
                {
                    LauncherLog.Log(
                        ex,
                        "加载 PCL 主页自定义信息失败",
                        LauncherRuntime.ModeDebug
                            ? LauncherLogLevel.Msgbox
                            : LauncherLogLevel.Hint,
                        userSummary: Lang.Text("Launch.Error.OperationFailed"));
                }
            }, $"刷新主页 #{LauncherRuntime.GetUuid()}");
    }

    private void RefreshReal()
    {
        var content = string.Empty;
        string? url;

        var homepageType = Config.Preference.Homepage.Type;

        switch (homepageType)
        {
            case 1:
            {
                // 本地文件
                LogWrapper.Info("[Page] 主页自定义数据来源：本地文件");

                var customPath = LauncherFileSystem.ResolvePath(
                    Path.Combine(
                        LauncherPaths.ExecutableDirectoryWithSlash,
                        "PCL",
                        "Custom.xaml"));

                var bytes = Files
                    .ReadAllBytesOrEmptyAsync(customPath)
                    .GetAwaiter()
                    .GetResult();

                content = EncodingUtils.DecodeBytes(bytes);
                break;
            }

            case 2:
            {
                // 网络文件
                url = Config.Preference.Homepage.CustomUrl;
                content = LoadFromNetwork(url);
                break;
            }

            case 3:
            {
                // 预设主页
                var preset = Config.Preference.Homepage.SelectedPreset;

                switch (preset)
                {
                    case 0:
                    {
                        LogWrapper.Info("[Page] 主页预设：你知道吗");
                        var hintText = GetRandomHint();

                        content = """
                                  <local:MyCard
                                      Title="{{DynamicResource Launch.Status.Trivia}}"
                                      Margin="0,0,0,15">
                                      <TextBlock
                                          Margin="25,38,23,15"
                                          FontSize="13.5"
                                          IsHitTestVisible="False"
                                          Text="{hintText}"
                                          TextWrapping="Wrap"
                                          Foreground="{{DynamicResource ColorBrush1}}" />

                                      <local:MyIconButton
                                          Height="22"
                                          Width="22"
                                          Margin="9"
                                          VerticalAlignment="Top"
                                          HorizontalAlignment="Right"
                                          EventType="刷新主页"
                                          EventData="/"
                                          SvgIcon="lucide/refresh-cw" />
                                  </local:MyCard>
                                  """;

                        break;
                    }

                    case 1:
                        LogWrapper.Info("[Page] 主页预设：回声洞 已被移除");
                        ModMain.MyMsgBox(
                            Lang.Text("Launch.Homepage.Preset.EchoCave.Removed"));
                        return;

                    case 2:
                        LoadPreset(
                            "Minecraft 新闻",
                            "https://news.bugjump.net");
                        break;

                    case 3:
                        LoadPreset(
                            "每日整合包推荐",
                            "https://pclsub.sodamc.com/");
                        break;

                    case 4:
                        LoadPreset(
                            "Minecraft 皮肤推荐",
                            "https://forgepixel.com/pcl_sub_file");
                        break;

                    case 5:
                        LoadPreset(
                            "OpenBMCLAPI 仪表盘 Lite",
                            "https://pcl-bmcl.milu.ink/");
                        break;

                    case 6:
                        LoadPreset(
                            "PCL 新功能说明书",
                            "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml");
                        break;

                    case 7:
                        LoadPreset(
                            "杂志主页",
                            "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml");
                        break;

                    case 8:
                        LoadPreset(
                            "PCL GitHub 仪表盘",
                            "https://ddf.pcl-community.org/Custom.xaml");
                        break;

                    case 9:
                        LoadPreset(
                            "Minecraft 更新摘要",
                            "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml");
                        break;

                    case 10:
                        LoadPreset(
                            "今日新闻热点",
                            "https://pcl.wyc-w.top/index.xaml");
                        break;

                    case 11:
                        LoadPreset(
                            "Minecraft 芝士站",
                            "https://www.xxag.top/mkss");
                        break;

                    case 12:
                        LoadPreset(
                            "整合包推荐引擎",
                            "https://qawsedrftgyhujiko.fun/pcl2/Custom.xaml");
                        break;

                    case 13:
                        LoadPreset(
                            "Bangumi 番剧主页",
                            "https://bangumi.p.kaphia.qzz.io");
                        break;

                    case 14:
                        LoadPreset(
                            "PCL CE 公告栏",
                            "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml");
                        break;

                    case 15:
                        LogWrapper.Info("[Page] 主页预设：Minecraft 信息流");

                        Dispatcher.Invoke(() =>
                        {
                            ModMain.frmHomepageNews ??= new PageHomepageNewsView();

                            PanCustom.Children.Clear();
                            PanCustom.Children.Add(ModMain.frmHomepageNews);
                        });

                        return;
                }

                break;
            }
        }

        UiThread.Post(() => LoadContent(content));
        return;

        void LoadPreset(string name, string presetUrl)
        {
            LogWrapper.Info($"[Page] 主页预设：{name}");

            url = presetUrl;
            content = LoadFromNetwork(url);
        }
    }

    /// <summary>
    ///     根据 URL 加载网络内容，优先使用缓存
    /// </summary>
    private string LoadFromNetwork(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        var cachePath = Path.Combine(LauncherPaths.TempWithSlash, "Cache", "Custom.xaml");
        var cachedUrl = (string)States.UI.SavedHomepageUrl;

        if (url == cachedUrl && File.Exists(cachePath))
        {
            LogWrapper.Info("[Page] 主页自定义数据来源：联网缓存文件");
            // 后台更新缓存
            onlineLoader.Start(url);
            return Files.ReadAllTextOrEmptyAsync(LauncherFileSystem.ResolvePath(cachePath)).GetAwaiter().GetResult();
        }

        LogWrapper.Info("[Page] 主页自定义数据来源：联网全新下载");
        HintWrapper.Show(Lang.Text("Launch.Homepage.Loading"));
        UiThread.Invoke(() => LoadContent("")); // 先清空页面
        States.UI.SavedHomepageVersion = "";
        onlineLoader.Start(url); // 下载完成后将会再次触发更新
        return "";
    }

    private readonly object refreshLock = new();

    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        string[]? lines = null;

        // 外部文件
        var externalPath = Path.Combine(LauncherPaths.ExecutableDirectoryWithSlash, "PCL", "hints.txt");
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
                LauncherLog.Log(
                    Lang.Text("Launch.Homepage.Error.ExternalFile", externalPath),
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Launch.Homepage.Error.ExternalFile", externalPath));
            }
        }

        // 嵌入式资源
        if (lines is null || lines.Length == 0)
        {
            var langCode = LocalizationService.CurrentLanguage.Code;
            lines = _LoadEmbeddedHints(langCode)
                ?? _LoadEmbeddedHints(LocalizationService.DefaultLanguageCode);
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

    private static string[]? _LoadEmbeddedHints(string langCode)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Plain Craft Launcher 2;component/Resources/hints/{langCode}.txt", UriKind.Absolute);
            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd()
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    // 联网获取主页文件
    private readonly ModLoader.LoaderTask<string, int> onlineLoader;

    private void OnlineLoaderSub(ModLoader.LoaderTask<string, int> task)
    {
        var address = task.input; // #3721 中连续触发两次导致内容变化
        try
        {
            // 获取版本校验地址
            string versionAddress;
            if (address.Contains(".xaml"))
            {
                versionAddress = address.Replace(".xaml", ".xaml.ini");
            }
            else
            {
                versionAddress = address.BeforeFirst("?");
                if (!versionAddress.EndsWith("/"))
                    versionAddress += "/";
                versionAddress += "version";
                if (address.Contains("?"))
                    versionAddress += "?" + address.AfterFirst("?");
            }

            // 校验版本
            var version = "";
            var needDownload = true;
            try
            {
                version = Requester.FetchString(versionAddress);
                if (version.Length > 1000)
                    throw new Exception($"获取的主页版本过长（{version.Length} 字符）");
                var currentVersion = States.UI.SavedHomepageVersion;
                if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(currentVersion) &&
                    (version ?? "") == (currentVersion ?? ""))
                {
                    LauncherLog.Log($"[Page] 当前缓存的主页已为最新，当前版本：{version}，检查源：{versionAddress}");
                    needDownload = false;
                }
                else
                {
                    LauncherLog.Log($"[Page] 需要下载联网主页，当前版本：{version}，检查源：{versionAddress}");
                }
            }
            catch (Exception exx)
            {
                LauncherLog.Log(exx, "联网获取主页版本失败", LauncherLogLevel.Developer);
                LauncherLog.Log($"[Page] 无法检查联网主页版本，将直接下载，检查源：{versionAddress}");
            }

            // 实际下载
            if (needDownload)
            {
                var fileContent = Requester.FetchString(address);
                LauncherLog.Log($"[Page] 已联网下载主页，内容长度：{fileContent.Length}，来源：{address}");
                States.UI.SavedHomepageUrl = address;
                States.UI.SavedHomepageVersion = version;
                Files.WriteFileAsync(LauncherFileSystem.ResolvePath(LauncherPaths.TempWithSlash + @"Cache\Custom.xaml"), fileContent).GetAwaiter().GetResult();
            }

            // 要求刷新
            UiThread.Post(Refresh); // 不直接调用 Refresh，以防止死循环（#6245）
        }
        catch (Exception ex)
        {
            LauncherLog.Log(
                ex,
                Lang.Text("Launch.Homepage.Error.Download", address),
                LauncherRuntime.ModeDebug
                    ? LauncherLogLevel.Msgbox
                    : LauncherLogLevel.Hint,
                userSummary: Lang.Text("Launch.Homepage.Error.Download", address));
        }
    }

    /// <summary>
    ///     立即强制刷新主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    public void ForceRefresh()
    {
        LauncherLog.Log("[Page] 要求强制刷新主页");
        ClearCache();
        // 实际的刷新
        if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.Launch)
        {
            PanBack.ScrollToHome();
            Refresh();
        }
        else
        {
            ModMain.frmMain.PageChange(FormMain.PageType.Launch);
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
        loadedContentHash = -1;
        onlineLoader.input = "";
        States.UI.SavedHomepageUrl = "";
        States.UI.SavedHomepageVersion = "";
        LauncherLog.Log("[Page] 已清空主页缓存");
    }

    /// <summary>
    ///     从文本内容中加载主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    private void LoadContent(string content)
    {
        lock (loadContentLock)
        {
            // 如果加载目标内容一致则不加载
            var hash = content.GetHashCode();
            if (hash == loadedContentHash)
            {
                _ApplyHomepageLivePatchesFromFile();
                return;
            }
            loadedContentHash = hash;
            // 实际加载内容
            PanCustom.Children.Clear();
            if (string.IsNullOrWhiteSpace(content))
            {
                LauncherLog.Log("[Page] 实例化：清空主页 UI，来源为空");
                return;
            }

            var loadStartTime = DateTime.Now;
            try
            {
                content = ModMain.ArgumentReplace(content);
                while (content.Contains("xmlns"))
                    content = content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");
                content =
                    $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{content}</StackPanel>";
                LauncherLog.Log($"[Page] 实例化：加载主页 UI 开始，最终内容长度：{content.Count()}");
                PanCustom.Children.Add((UIElement)CustomXamlLoader.Load(content, out var sanitizeResult));
                _ShowSanitizeHints(sanitizeResult);
                _ApplyHomepageLivePatchesFromFile();
            }
            catch (Exception ex)
            {
                if (LauncherRuntime.ModeDebug)
                {
                    LauncherLog.Log(ex, $"加载失败的主页内容：\r\n{content}");
                    if (ModMain.MyMsgBox(
                            ex is UnauthorizedAccessException
                                ? ex.Message
                                : Lang.Text("Launch.Homepage.LoadFailed.Message", ex),
                            Lang.Text("Launch.Homepage.LoadFailed.Title"),
                            Lang.Text("Launch.Homepage.LoadFailed.Retry"),
                            Lang.Text("Common.Action.Cancel")) ==
                        1) goto Refresh; // 防止 SyncLock 死锁
                }
                else
                {
                    LauncherLog.Log(
                        ex,
                        Lang.Text("Launch.Homepage.LoadFailed.Title"),
                        LauncherLogLevel.Hint,
                        userSummary: Lang.Text("Launch.Homepage.LoadFailed.Title"));
                }

                return;
            }

            var loadCostTime = (DateTime.Now - loadStartTime).Milliseconds;
            LauncherLog.Log($"[Page] 实例化：加载主页 UI 完成，耗时 {loadCostTime}ms");
            if (loadCostTime > 3000)
                HintService.Hint(Lang.Text("Launch.Homepage.SlowWarning", Lang.Number(Math.Round(loadCostTime / 1000d, 1), "N1")));
        }

        return;
        Refresh: ;

        ForceRefresh();
    }

    private int loadedContentHash = -1;
    private readonly object loadContentLock = new();
    private static void _ShowSanitizeHints(XamlEventSanitizer.SanitizeResult result)
    {
        foreach (var unsupported in result.UnsupportedTypesFound)
            HintService.Hint(Lang.Text("Event.Sanitize.UnsupportedTypeHint", unsupported), HintType.Error);

        foreach (var unknown in result.UnrecognizedTypes)
            HintService.Hint(Lang.Text("Event.Sanitize.UnknownTypeHint", unknown), HintType.Error);
    }

    private const string homepageLivePatchFileName = "CustomLive.json";
    private const string homepageLiveSupportFileName = "CustomLive.supported.json";
    // Keep the reflection patch surface explicit because patch files are written by external tools.
    private static readonly Dictionary<string, string> _homepageLiveAllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "Text",
        ["title"] = "Title",
        ["info"] = "Info",
        ["tooltip"] = "ToolTip",
        ["visibility"] = "Visibility",
        ["isEnabled"] = "IsEnabled",
        ["opacity"] = "Opacity"
    };
    private FileSystemWatcher? _homepageLiveWatcher;
    private DispatcherTimer? _homepageLivePatchTimer;

    private void _EnsureHomepageLiveWatcher()
    {
        if (_homepageLiveWatcher != null) return;
        if ((int)Config.Preference.Homepage.Type != 1) return;

        try
        {
            var directory = _GetHomepageLiveDirectory();
            Directory.CreateDirectory(directory);
            _WriteHomepageLiveSupportMarker(directory);

            _homepageLiveWatcher = new FileSystemWatcher(directory, homepageLivePatchFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _homepageLiveWatcher.Changed += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Created += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Renamed += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.EnableRaisingEvents = true;
            _QueueHomepageLivePatchApply();
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[Page] Failed to start custom homepage live patch watcher", LauncherLogLevel.Developer);
        }
    }

    private void _DisposeHomepageLiveWatcher()
    {
        try
        {
            _homepageLiveWatcher?.Dispose();
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[Page] Failed to dispose custom homepage live patch watcher", LauncherLogLevel.Developer);
        }

        _homepageLiveWatcher = null;

        try
        {
            if (_homepageLivePatchTimer != null)
            {
                _homepageLivePatchTimer.Stop();
                _homepageLivePatchTimer.Tick -= _HomepageLivePatchTimerTick;
                _homepageLivePatchTimer = null;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[Page] Failed to dispose custom homepage live patch debounce timer", LauncherLogLevel.Developer);
        }

        _DeleteHomepageLiveSupportMarker();
    }

    private void _QueueHomepageLivePatchApply()
    {
        UiThread.Post(() =>
        {
            _homepageLivePatchTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _homepageLivePatchTimer.Tick -= _HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Tick += _HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Stop();
            _homepageLivePatchTimer.Start();
        });
    }

    private void _HomepageLivePatchTimerTick(object? sender, EventArgs e)
    {
        _homepageLivePatchTimer?.Stop();
        _ApplyHomepageLivePatchesFromFile();
    }

    private void _ApplyHomepageLivePatchesFromFile()
    {
        if (PanCustom.Children.Count == 0) return;
        if ((int)Config.Preference.Homepage.Type != 1) return;

        var file = Path.Combine(_GetHomepageLiveDirectory(), homepageLivePatchFileName);
        if (!File.Exists(file)) return;

        try
        {
            var token = JsonNode.Parse(_ReadHomepageLivePatchFile(file),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true });
            foreach (var patch in _EnumerateHomepageLivePatches(token))
                _ApplyHomepageLivePatch(patch);
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[Page] Failed to apply custom homepage live patches", LauncherLogLevel.Developer);
        }
    }

    private static string _ReadHomepageLivePatchFile(string file)
    {
        Exception? lastException = null;
        for (var i = 0; i < 3; i++)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                lastException = ex;
                Thread.Sleep(50);
            }
        }

        throw lastException ?? new IOException("Unable to read custom homepage live patch file.");
    }

    private static string _GetHomepageLiveDirectory()
    {
        return Path.Combine(LauncherPaths.ExecutableDirectoryWithSlash, "PCL");
    }

    private static void _WriteHomepageLiveSupportMarker(string directory)
    {
        try
        {
            var marker = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true })
            {
                ["processId"] = Environment.ProcessId,
                ["processPath"] = Environment.ProcessPath ?? "",
                ["patchFile"] = homepageLivePatchFileName,
                ["startedAt"] = DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
            };
            File.WriteAllText(Path.Combine(directory, homepageLiveSupportFileName), marker.ToJsonString());
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[Page] Failed to write custom homepage live patch support marker", LauncherLogLevel.Developer);
        }
    }

    private static void _DeleteHomepageLiveSupportMarker()
    {
        try
        {
            var file = Path.Combine(_GetHomepageLiveDirectory(), homepageLiveSupportFileName);
            if (!File.Exists(file)) return;

            var marker = (JsonObject)JsonNode.Parse(_ReadHomepageLivePatchFile(file),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true })!;
            if (marker["processId"]?.GetValue<int>() == Environment.ProcessId)
                File.Delete(file);
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "[Page] Failed to delete custom homepage live patch support marker", LauncherLogLevel.Developer);
        }
    }

    private static IEnumerable<JsonObject> _EnumerateHomepageLivePatches(JsonNode token)
    {
        if (token is JsonObject obj)
        {
            if (obj["patches"] is JsonArray patches)
            {
                foreach (var patch in patches.OfType<JsonObject>())
                    yield return patch;
                yield break;
            }

            if (_TryGetString(obj, "target", "tag", "name") != null)
            {
                yield return obj;
                yield break;
            }

            foreach (var property in obj)
            {
                if (property.Value is not JsonObject patch) continue;
                patch = (JsonObject)JsonNode.Parse(patch.ToJsonString(),
                    new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true })!;
                patch["target"] ??= property.Key;
                yield return patch;
            }
        }
        else if (token is JsonArray array)
        {
            foreach (var patch in array.OfType<JsonObject>())
                yield return patch;
        }
    }

    private void _ApplyHomepageLivePatch(JsonObject patch)
    {
        var target = _TryGetString(patch, "target", "tag", "name");
        if (string.IsNullOrWhiteSpace(target)) return;

        foreach (var element in _FindElementsByTag(PanCustom, target))
            _ApplyHomepageLivePatchToElement(element, patch);
    }

    private void _ApplyHomepageLivePatchToElement(FrameworkElement element, JsonObject patch)
    {
        _SetPropertyIfPresent(element, patch, "text", "Text");
        _SetPropertyIfPresent(element, patch, "title", "Title");
        _SetPropertyIfPresent(element, patch, "info", "Info");
        _SetPropertyIfPresent(element, patch, "tooltip", "ToolTip");
        _SetPropertyIfPresent(element, patch, "toolTip", "ToolTip");
        _SetPropertyIfPresent(element, patch, "visibility", "Visibility");
        _SetPropertyIfPresent(element, patch, "isEnabled", "IsEnabled");
        _SetPropertyIfPresent(element, patch, "opacity", "Opacity");

        if (patch["properties"] is JsonObject properties)
        {
            foreach (var property in properties)
                _TrySetElementProperty(element, property.Key, property.Value?.ToString() ?? "");
        }

        var childrenXaml = _TryGetString(patch, "childrenXaml", "ChildrenXaml");
        if (!string.IsNullOrEmpty(childrenXaml) && element is Panel panel)
            _ReplacePanelChildren(panel, childrenXaml);
    }

    private static void _SetPropertyIfPresent(FrameworkElement element, JsonObject patch, string jsonName, string propertyName)
    {
        if (patch.TryGetPropertyValue(jsonName, out var value))
            _TrySetElementProperty(element, propertyName, value?.ToString() ?? "");
    }

    private static bool _TrySetElementProperty(FrameworkElement element, string propertyName, string value)
    {
        if (!_homepageLiveAllowedProperties.TryGetValue(propertyName, out var allowedPropertyName))
        {
            LauncherLog.Log($"[Page] Skipped unsupported live patch property {propertyName}", LauncherLogLevel.Developer);
            return false;
        }

        propertyName = allowedPropertyName;
        var property = element.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanWrite) return false;

        try
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var trimmedValue = value.Trim();
            object convertedValue;
            if (propertyType == typeof(string))
                convertedValue = value;
            else if (propertyType == typeof(object))
                convertedValue = value;
            else if (propertyType == typeof(bool) && bool.TryParse(trimmedValue, out var boolValue))
                convertedValue = boolValue;
            else if (propertyType == typeof(int) && int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                convertedValue = intValue;
            else if (propertyType == typeof(double) && double.TryParse(trimmedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                convertedValue = doubleValue;
            else if (propertyType == typeof(Visibility))
            {
                if (!Enum.TryParse(trimmedValue, true, out Visibility visibilityValue))
                    return false;
                convertedValue = visibilityValue;
            }
            else if (propertyType.IsEnum && Enum.TryParse(propertyType, trimmedValue, true, out var enumValue))
                convertedValue = enumValue;
            else
                return false;

            property.SetValue(element, convertedValue);
            return true;
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, $"[Page] Failed to set live patch property {propertyName}", LauncherLogLevel.Developer);
            return false;
        }
    }

    private static void _ReplacePanelChildren(Panel panel, string childrenXaml)
    {
        var content = ModMain.ArgumentReplace(childrenXaml);
        while (content.Contains("xmlns"))
            content = content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");

        var wrapped =
            $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{content}</StackPanel>";

        if (CustomXamlLoader.Load(wrapped) is not Panel parsedPanel) return;

        var children = parsedPanel.Children.OfType<UIElement>().ToList();
        parsedPanel.Children.Clear();
        panel.Children.Clear();
        foreach (var child in children)
            panel.Children.Add(child);
    }

    private static IEnumerable<FrameworkElement> _FindElementsByTag(DependencyObject root, string tag)
    {
        if (root is FrameworkElement element &&
            string.Equals(element.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            yield return element;

        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            foreach (var child in _FindElementsByTag(VisualTreeHelper.GetChild(root, i), tag))
                yield return child;
        }
    }

    private static string? _TryGetString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var value))
                return value?.ToString();
        }

        return null;
    }

    #endregion
}
