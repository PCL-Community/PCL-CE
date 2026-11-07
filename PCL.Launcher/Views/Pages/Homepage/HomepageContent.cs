using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Network;
using PCL.Xaml;

namespace PCL.Views.Pages.Homepage;

/// <summary>
///     主页内容的解析结果：描述当前配置下主页应当展示什么，以及内容来自哪里。
///     运行时 XAML 主页在 <see cref="Xaml" /> 中携带原始内容，由白名单渲染器转换；
///     联网主页在没有缓存时返回 <see cref="IsLoading" />，由界面层清空旧页并等待后台下载。
/// </summary>
public sealed record HomepageContent
{
    public required HomepageSourceKind Kind { get; init; }

    public string? TitleKey { get; init; }

    public string? TriviaText { get; init; }

    public string? SourceText { get; init; }

    /// <summary>运行时 XAML 内容；仅 <see cref="HomepageSourceKind.CustomXaml" /> 使用。</summary>
    public string? Xaml { get; init; }

    public long ContentLength { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>正在等待联网下载（此时没有可渲染内容）。</summary>
    public bool IsLoading { get; init; }
}

/// <summary>
///     主页来源解析：读取 <c>Preference.Homepage</c> 配置，把主页来源解释为 <see cref="HomepageContent" />。
///     联网来源带本地缓存与版本校验，缓存地址为临时目录下的 <c>Cache/Custom.xaml</c>，
///     下载后的地址与版本写入 <c>States.UI.SavedHomepageUrl</c> / <c>SavedHomepageVersion</c>。
///     有缓存时先返回缓存并在后台检查新版本；没有缓存时返回加载态并后台下载。
///     后台刷新失败时会保留缓存/旧页，只有全新下载且没有可回退内容时才显示错误。
/// </summary>
public static class HomepageSource
{
    private const string CustomFileCacheName = "Custom.xaml";

    private const int NetworkTimeout = 10 * 60 * 1000;

    private const int MaxContentLength = 2 * 1024 * 1024;

    private static readonly Lock CacheLock = new();

    public static string CachePath => Path.Combine(Paths.Temp, "Cache", CustomFileCacheName);

    public static void ClearCache()
    {
        States.UI.SavedHomepageUrl = "";
        States.UI.SavedHomepageVersion = "";
        try
        {
            lock (CacheLock)
            {
                if (File.Exists(CachePath))
                    File.Delete(CachePath);
                var tempPath = CachePath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "清空主页缓存失败");
        }
    }

    /// <summary>
    ///     解析当前配置对应的主页内容。该方法会读取本地文件并可能启动后台联网刷新，
    ///     应在后台线程调用；<paramref name="onBackgroundUpdated" /> 在后台线程触发。
    /// </summary>
    /// <param name="onNetworkDownload">开始联网下载时触发，供界面层提示加载中；使用缓存刷新时不触发。</param>
    /// <param name="onBackgroundUpdated">后台刷新完成（成功或失败）时触发，供界面层替换或保留内容。</param>
    public static HomepageContent Resolve(
        Action? onNetworkDownload = null,
        Action<HomepageContent>? onBackgroundUpdated = null)
    {
        var type = Config.Preference.Homepage.Type;
        return type switch
        {
            1 => ResolveLocalFile(),
            2 => ResolveRemote(
                Config.Preference.Homepage.CustomUrl,
                onNetworkDownload,
                onBackgroundUpdated),
            3 => ResolvePreset(
                Config.Preference.Homepage.SelectedPreset,
                onNetworkDownload,
                onBackgroundUpdated),
            _ => new HomepageContent
            {
                Kind = HomepageSourceKind.Blank
            }
        };
    }

    private static HomepageContent ResolveLocalFile()
    {
        var path = Path.Combine(Paths.Data, "Custom.xaml");
        LogWrapper.Info("Launch", "主页自定义数据来源：本地文件");
        if (!File.Exists(path))
            return new HomepageContent
            {
                Kind = HomepageSourceKind.CustomXaml,
                SourceText = path,
                ErrorMessage = Lang.Text("PageLaunch.Homepage.Custom.Error.Missing")
            };

        try
        {
            var content = File.ReadAllText(path);
            if (content.Length > MaxContentLength)
                return new HomepageContent
                {
                    Kind = HomepageSourceKind.CustomXaml,
                    SourceText = path,
                    ErrorMessage = Lang.Text(
                        "Launch.Homepage.LoadFailed.Message",
                        $"文件过大（{content.Length} 字符，上限 {MaxContentLength} 字符）")
                };
            if (string.IsNullOrWhiteSpace(content))
                return new HomepageContent
                {
                    Kind = HomepageSourceKind.CustomXaml,
                    SourceText = path,
                    ErrorMessage = Lang.Text("Launch.Homepage.LoadFailed.Message", "主页文件为空")
                };

            return new HomepageContent
            {
                Kind = HomepageSourceKind.CustomXaml,
                SourceText = path,
                Xaml = content,
                ContentLength = content.Length
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Launch", "读取本地自定义主页失败");
            return new HomepageContent
            {
                Kind = HomepageSourceKind.CustomXaml,
                SourceText = path,
                ErrorMessage = Lang.Text("Launch.Homepage.Error.ExternalFile", path)
            };
        }
    }

    private static HomepageContent ResolvePreset(
        int index,
        Action? onNetworkDownload,
        Action<HomepageContent>? onBackgroundUpdated)
    {
        var preset = HomepagePresetTable.Find(index);
        if (preset is null)
        {
            LogWrapper.Info("Launch", $"主页预设索引无效：{index}，按空白主页处理");
            return new HomepageContent
            {
                Kind = HomepageSourceKind.Blank
            };
        }

        return preset.Kind switch
        {
            HomepageSourceKind.Trivia => new HomepageContent
            {
                Kind = HomepageSourceKind.Trivia,
                TitleKey = preset.NameKey,
                TriviaText = HomepageHints.GetRandomHint(raw: true)
            },
            HomepageSourceKind.Removed => new HomepageContent
            {
                Kind = HomepageSourceKind.Removed,
                TitleKey = preset.NameKey
            },
            HomepageSourceKind.NewsFeed => new HomepageContent
            {
                Kind = HomepageSourceKind.NewsFeed,
                TitleKey = preset.NameKey
            },
            _ => ResolvePresetRemote(preset, onNetworkDownload, onBackgroundUpdated)
        };
    }

    private static HomepageContent ResolvePresetRemote(
        HomepagePreset preset,
        Action? onNetworkDownload,
        Action<HomepageContent>? onBackgroundUpdated)
    {
        var content = ResolveRemote(
            preset.Url,
            onNetworkDownload,
            updated =>
            {
                if (onBackgroundUpdated is null)
                    return;
                onBackgroundUpdated(
                    updated with
                    {
                        TitleKey = preset.NameKey
                    });
            });
        return content with
        {
            TitleKey = preset.NameKey
        };
    }

    private static HomepageContent ResolveRemote(
        string? url,
        Action? onNetworkDownload,
        Action<HomepageContent>? onBackgroundUpdated)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new HomepageContent
            {
                Kind = HomepageSourceKind.Blank
            };

        var cachedUrl = States.UI.SavedHomepageUrl;
        var cacheExists = File.Exists(CachePath);
        if (url == cachedUrl && cacheExists)
            try
            {
                string cached;
                lock (CacheLock)
                {
                    cached = File.ReadAllText(CachePath);
                }

                if (!string.IsNullOrWhiteSpace(cached))
                {
                    LogWrapper.Info("Launch", $"[Page] 主页自定义数据来源：联网缓存文件（{url}）");
                    StartBackgroundRefresh(url, onBackgroundUpdated, false);
                    return new HomepageContent
                    {
                        Kind = HomepageSourceKind.CustomXaml,
                        SourceText = url,
                        Xaml = cached,
                        ContentLength = cached.Length
                    };
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "Launch", $"读取主页缓存失败：{url}");
            }

        LogWrapper.Info("Launch", $"[Page] 主页自定义数据来源：联网全新下载（{url}）");
        onNetworkDownload?.Invoke();
        StartBackgroundRefresh(url, onBackgroundUpdated, true);
        return new HomepageContent
        {
            Kind = HomepageSourceKind.CustomXaml,
            SourceText = url,
            IsLoading = true
        };
    }

    private static void StartBackgroundRefresh(
        string url,
        Action<HomepageContent>? onBackgroundUpdated,
        bool forceDownload)
    {
        Task.Run(() =>
        {
            HomepageContent updated;
            try
            {
                var cachedVersion = States.UI.SavedHomepageVersion;
                var version = TryFetchVersion(url);
                if (!forceDownload
                    && !string.IsNullOrEmpty(version)
                    && !string.IsNullOrEmpty(cachedVersion)
                    && version == cachedVersion)
                {
                    LogWrapper.Info(
                        "Launch",
                        $"[Page] 当前缓存的主页已为最新，当前版本：{version}");
                    return;
                }

                var content = Requester.FetchString(
                    url,
                    new RequestParam
                    {
                        Timeout = NetworkTimeout,
                        Retries = 1
                    });
                if (string.IsNullOrWhiteSpace(content))
                    throw new InvalidDataException("下载到的主页内容为空");
                if (content.Length > MaxContentLength)
                    throw new InvalidDataException(
                        $"下载到的主页内容过大（{content.Length} 字符，上限 {MaxContentLength} 字符）");

                var security = XamlSecurityValidator.ValidateFragment(content);
                if (!security.IsValid)
                    throw new InvalidDataException(
                        "下载到的主页未通过安全校验："
                        + string.Join("；", security.Errors));
                if (security.ElementCount == 0)
                    throw new InvalidDataException("下载到的主页不是有效的 XAML 界面");

                lock (CacheLock)
                {
                    var directory = Path.GetDirectoryName(CachePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    var tempPath = CachePath + ".tmp";
                    File.WriteAllText(tempPath, content);
                    File.Move(tempPath, CachePath, true);
                }

                States.UI.SavedHomepageUrl = url;
                States.UI.SavedHomepageVersion = version;
                LogWrapper.Info(
                    "Launch",
                    $"[Page] 已联网下载主页，内容长度：{content.Length}，来源：{url}");
                updated = new HomepageContent
                {
                    Kind = HomepageSourceKind.CustomXaml,
                    SourceText = url,
                    Xaml = content,
                    ContentLength = content.Length
                };
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "Launch", $"获取联网主页失败：{url}");
                updated = new HomepageContent
                {
                    Kind = HomepageSourceKind.CustomXaml,
                    SourceText = url,
                    ErrorMessage = ex is InvalidDataException
                        ? Lang.Text("Launch.Homepage.LoadFailed.Message", ex.Message)
                        : Lang.Text("Launch.Homepage.Error.Download", url)
                };
            }

            onBackgroundUpdated?.Invoke(updated);
        });
    }

    private static string VersionAddress(string url)
    {
        if (url.Contains(".xaml", StringComparison.OrdinalIgnoreCase))
            return url.Replace(".xaml", ".xaml.ini", StringComparison.OrdinalIgnoreCase);

        var versionAddress = url.Split('?')[0];
        if (!versionAddress.EndsWith('/'))
            versionAddress += "/";
        versionAddress += "version";
        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
            versionAddress += url[queryIndex..];
        return versionAddress;
    }

    private static string TryFetchVersion(string url)
    {
        try
        {
            var version = Requester.FetchString(
                VersionAddress(url),
                new RequestParam
                {
                    Timeout = 30_000,
                    Retries = 1
                });
            return version.Length > 1000
                ? throw new InvalidOperationException($"获取的主页版本过长（{version.Length} 字符）")
                : version;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "联网获取主页版本失败，将直接下载");
            return "";
        }
    }
}