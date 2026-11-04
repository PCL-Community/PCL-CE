using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Network;

namespace PCL.Views.Pages.Homepage;

/// <summary>
///     主页内容的解析结果：描述当前配置下主页应当展示什么，以及内容来自哪里。
///     运行时 XAML 主页无法被直接渲染，因此只保留来源信息，由界面层给出降级说明。
/// </summary>
public sealed record HomepageContent
{
    public required HomepageSourceKind Kind { get; init; }

    public string? TitleKey { get; init; }

    public string? TriviaText { get; init; }

    public string? SourceText { get; init; }

    public long ContentLength { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>
///     主页来源解析：读取 <c>Preference.Homepage</c> 配置，把主页来源解释为 <see cref="HomepageContent" />。
///     联网来源带本地缓存与版本校验，缓存地址为临时目录下的 <c>Cache/Custom.xaml</c>，
///     下载后的地址与版本写入 <c>States.UI.SavedHomepageUrl</c> / <c>SavedHomepageVersion</c>。
/// </summary>
public static class HomepageSource
{
    private const string CustomFileCacheName = "Custom.xaml";

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
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "清空主页缓存失败");
        }
    }

    /// <summary>
    ///     解析当前配置对应的主页内容。该方法会进行文件读取与联网请求，应在后台线程调用。
    /// </summary>
    /// <param name="onNetworkDownload">开始联网下载时触发，供界面层提示加载中；不需要下载时为 <c>null</c>。</param>
    public static HomepageContent Resolve(Action? onNetworkDownload = null)
    {
        var type = Config.Preference.Homepage.Type;
        return type switch
        {
            1 => ResolveLocalFile(),
            2 => ResolveRemote(Config.Preference.Homepage.CustomUrl, onNetworkDownload),
            3 => ResolvePreset(Config.Preference.Homepage.SelectedPreset, onNetworkDownload),
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
            return new HomepageContent
            {
                Kind = HomepageSourceKind.CustomXaml,
                SourceText = path,
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

    private static HomepageContent ResolvePreset(int index, Action? onNetworkDownload)
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
                TriviaText = HomepageHints.GetRandomHint()
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
            _ => ResolveRemote(preset.Url, onNetworkDownload) with
            {
                TitleKey = preset.NameKey
            }
        };
    }

    private static HomepageContent ResolveRemote(string? url, Action? onNetworkDownload)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new HomepageContent
            {
                Kind = HomepageSourceKind.Blank
            };

        try
        {
            var cachedUrl = States.UI.SavedHomepageUrl;
            var cacheExists = File.Exists(CachePath);
            if (url == cachedUrl && cacheExists)
            {
                var version = TryFetchVersion(url);
                var cachedVersion = States.UI.SavedHomepageVersion;
                if (string.IsNullOrEmpty(version)
                    || string.IsNullOrEmpty(cachedVersion)
                    || version == cachedVersion)
                {
                    LogWrapper.Info("Launch", $"[Page] 主页自定义数据来源：联网缓存文件（{url}）");
                    return new HomepageContent
                    {
                        Kind = HomepageSourceKind.CustomXaml,
                        SourceText = url,
                        ContentLength = new FileInfo(CachePath).Length
                    };
                }
            }

            LogWrapper.Info("Launch", $"[Page] 主页自定义数据来源：联网全新下载（{url}）");
            onNetworkDownload?.Invoke();
            var versionToSave = TryFetchVersion(url);
            var content = Requester.FetchString(url);
            lock (CacheLock)
            {
                var directory = Path.GetDirectoryName(CachePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(CachePath, content);
            }

            States.UI.SavedHomepageUrl = url;
            States.UI.SavedHomepageVersion = versionToSave;
            return new HomepageContent
            {
                Kind = HomepageSourceKind.CustomXaml,
                SourceText = url,
                ContentLength = content.Length
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", $"获取联网主页失败：{url}");
            return new HomepageContent
            {
                Kind = HomepageSourceKind.CustomXaml,
                SourceText = url,
                ErrorMessage = Lang.Text("Launch.Homepage.Error.Download", url)
            };
        }
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
            var version = Requester.FetchString(VersionAddress(url));
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