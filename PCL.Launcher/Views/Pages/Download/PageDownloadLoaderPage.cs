using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>下载页的加载器种类。</summary>
internal enum DownloadLoaderKind
{
    OptiFine,
    Forge,
    NeoForge,
    Cleanroom,
    Fabric,
    LegacyFabric,
    LabyMod,
    LiteLoader
}

/// <summary>加载器页面的元数据：标题、简介、官网与版本列表加载器。</summary>
internal static class DownloadLoaderMeta
{
    public static string GetTitleKey(DownloadLoaderKind kind)
    {
        return kind switch
        {
            DownloadLoaderKind.OptiFine => "Download.Version.Optifine.Intro.Title",
            DownloadLoaderKind.Forge => "Download.Version.Forge.Intro.Title",
            DownloadLoaderKind.NeoForge => "Download.Version.NeoForge.Intro.Title",
            DownloadLoaderKind.Cleanroom => "Download.Version.Cleanroom.Intro.Title",
            DownloadLoaderKind.Fabric => "Download.Version.Fabric.Intro.Title",
            DownloadLoaderKind.LegacyFabric => "Download.Version.LegacyFabric.Intro.Title",
            DownloadLoaderKind.LabyMod => "Download.Version.LabyMod.Intro.Title",
            _ => "Download.Version.LiteLoader.Intro.Title"
        };
    }

    public static string GetDescriptionKey(DownloadLoaderKind kind)
    {
        return kind switch
        {
            DownloadLoaderKind.OptiFine => "Download.Version.Optifine.Intro.Description",
            DownloadLoaderKind.Forge => "Download.Version.Forge.Intro.Description",
            DownloadLoaderKind.NeoForge => "Download.Version.NeoForge.Intro.Description",
            DownloadLoaderKind.Cleanroom => "Download.Version.Cleanroom.Intro.Description",
            DownloadLoaderKind.Fabric => "Download.Version.Fabric.Intro.Description",
            DownloadLoaderKind.LegacyFabric => "Download.Version.LegacyFabric.Intro.Description",
            DownloadLoaderKind.LabyMod => "Download.Version.LabyMod.Intro.Description",
            _ => "Download.Version.LiteLoader.Intro.Description"
        };
    }

    public static string GetLoadingKey(DownloadLoaderKind kind)
    {
        return kind switch
        {
            DownloadLoaderKind.OptiFine => "Download.Version.Optifine.LoadingList",
            DownloadLoaderKind.Forge => "Download.Version.Forge.LoadingList",
            DownloadLoaderKind.NeoForge => "Download.Version.NeoForge.LoadingList",
            DownloadLoaderKind.Cleanroom => "Download.Version.Cleanroom.LoadingList",
            DownloadLoaderKind.Fabric => "Download.Version.Fabric.LoadingList",
            DownloadLoaderKind.LegacyFabric => "Download.Version.LegacyFabric.LoadingList",
            DownloadLoaderKind.LabyMod => "Download.Version.LabyMod.LoadingList",
            _ => "Download.Version.LiteLoader.LoadingList"
        };
    }

    public static string GetWebsite(DownloadLoaderKind kind)
    {
        return kind switch
        {
            DownloadLoaderKind.OptiFine => "https://www.optifine.net/",
            DownloadLoaderKind.Forge => "https://files.minecraftforge.net",
            DownloadLoaderKind.NeoForge => "https://neoforged.net/",
            DownloadLoaderKind.Cleanroom => "https://github.com/CleanroomMC/Cleanroom",
            DownloadLoaderKind.Fabric => "https://fabricmc.net",
            DownloadLoaderKind.LegacyFabric => "https://legacyfabric.net/",
            DownloadLoaderKind.LabyMod => "https://labymod.net/",
            _ => "https://www.liteloader.com"
        };
    }

    public static Loading.LoaderBase GetListLoader(DownloadLoaderKind kind)
    {
        return kind switch
        {
            DownloadLoaderKind.OptiFine => GameDownload.DlOptiFineListLoader,
            DownloadLoaderKind.Forge => GameDownload.DlForgeListLoader,
            DownloadLoaderKind.NeoForge => GameDownload.DlNeoForgeListLoader,
            DownloadLoaderKind.Cleanroom => GameDownload.DlCleanroomListLoader,
            DownloadLoaderKind.Fabric => GameDownload.DlFabricListLoader,
            DownloadLoaderKind.LegacyFabric => GameDownload.DlLegacyFabricListLoader,
            DownloadLoaderKind.LabyMod => GameDownload.DlLabyModListLoader,
            _ => GameDownload.DlLiteLoaderListLoader
        };
    }
}

/// <summary>
///     加载器下载子页：展示简介与官方版本列表，点击版本可保存对应的安装器 Jar。
///     列表与安装器下载使用业务层的真实加载器完成，不依赖安装管线。
/// </summary>
internal sealed class PageDownloadLoaderPage : MyPageRight, IPageView, IRefreshable
{
    private readonly MyCard _cardTip;
    private readonly DownloadLoaderKind _kind;
    private readonly MyLoading _load;
    private readonly MyScrollViewer _panBack;
    private readonly MyCard _panLoad;
    private readonly StackPanel _panMain;

    public PageDownloadLoaderPage(DownloadLoaderKind kind)
    {
        _kind = kind;

        _panMain = new StackPanel
        {
            Margin = new Thickness(20d, 15d, 20d, 20d)
        };
        _load = new MyLoading
        {
            Margin = new Thickness(20d, 20d, 20d, 17d),
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = Lang.Text(DownloadLoaderMeta.GetLoadingKey(kind))
        };
        _panLoad = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(40d, 50d, 40d, 0d),
            UseAnimation = false
        };
        _panLoad.Children.Add(_load);
        _cardTip = CreateIntroCard(kind);

        var stack = new StackPanel
        {
            Margin = new Thickness(5d)
        };
        stack.Children.Add(_cardTip);
        stack.Children.Add(_panMain);
        stack.Children.Add(_panLoad);

        _panBack = new MyScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
        Child = _panBack;
        PanScroll = _panBack;

        PageLoaderInit(
            _load,
            _panLoad,
            _panMain,
            _cardTip,
            DownloadLoaderMeta.GetListLoader(kind),
            _ => OnLoadFinished());
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _panBack.Offset = default;
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        DownloadLoaderMeta.GetListLoader(_kind).Start(null, true);
    }

    protected override void RefreshLocalization()
    {
        _load.Text = Lang.Text(DownloadLoaderMeta.GetLoadingKey(_kind));
        _cardTip.Title = Lang.Text(DownloadLoaderMeta.GetTitleKey(_kind));
    }

    private static MyCard CreateIntroCard(DownloadLoaderKind kind)
    {
        var card = new MyCard
        {
            Margin = new Thickness(20d, 20d, 20d, 0d),
            Title = Lang.Text(DownloadLoaderMeta.GetTitleKey(kind))
        };
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 15d, 20d),
            Spacing = 11d
        };
        panel.Children.Add(
            new TextBlock
            {
                Text = Lang.Text(DownloadLoaderMeta.GetDescriptionKey(kind)),
                TextWrapping = TextWrapping.Wrap
            });
        var websiteButton = new MyButton
        {
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d, 13d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = Lang.Text("Download.Version.OpenWebsite"),
            ColorType = MyButton.ColorState.Highlight
        };
        var website = DownloadLoaderMeta.GetWebsite(kind);
        websiteButton.Click += (_, _) => LauncherUi.Interaction?.OpenWebsite(website);
        panel.Children.Add(websiteButton);
        card.Children.Add(panel);
        return card;
    }

    private void OnLoadFinished()
    {
        try
        {
            _panMain.Children.Clear();
            switch (_kind)
            {
                case DownloadLoaderKind.Fabric:
                case DownloadLoaderKind.LegacyFabric:
                    BuildInstallerJsonList();
                    break;
                case DownloadLoaderKind.NeoForge:
                    BuildForgelikeList(GameDownload.DlNeoForgeListLoader.output.Value);
                    break;
                default:
                    BuildSimpleList();
                    break;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "可视化加载器版本列表出错");
            HintService.Show(Hints.Error(Lang.Text("Download.Error.OperationFailed")));
        }
    }

    private void BuildInstallerJsonList()
    {
        var result = _kind == DownloadLoaderKind.Fabric
            ? GameDownload.DlFabricListLoader.output.Value
            : GameDownload.DlLegacyFabricListLoader.output.Value;
        if (result?["installer"] is not JsonArray installers)
            return;

        var card = new MyCard
        {
            Margin = new Thickness(0d, 0d, 0d, 15d),
            Title = Lang.Text("Download.Version.VersionListCount", installers.Count)
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
        };
        foreach (var node in installers)
        {
            if (node is not JsonObject entry)
                continue;
            var version = entry["version"]?.ToString()?.Replace("+build", "") ?? "";
            var item = new MyListItem
            {
                Title = version,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Logo = LauncherPaths.ImageRoot + "Blocks/Fabric.png",
                LogoScale = 0.9,
                Info = entry["stable"]?.GetValue<bool>() == true
                    ? Lang.Text("Download.Version.Type.Stable")
                    : Lang.Text("Download.Version.Type.Preview"),
                tag = entry
            };
            item.Click += (_, _) => SaveInstaller(
                [entry["url"]?.ToString() ?? ""],
                LauncherCommon.GetFileNameFromPath(entry["url"]?.ToString() ?? version),
                _kind == DownloadLoaderKind.Fabric
                    ? "Download.Version.Installer.Fabric.Filter"
                    : "Download.Version.Installer.LegacyFabric.Filter");
            stack.Children.Add(item);
        }

        card.Children.Add(stack);
        _panMain.Children.Add(card);
    }

    private void BuildForgelikeList(List<GameDownload.DlNeoForgeListEntry> entries)
    {
        if (entries is null)
            return;
        foreach (var group in entries
                     .GroupBy(entry => entry.Inherit)
                     .OrderByDescending(group => group.Key))
        {
            var card = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title = $"{group.Key} ({group.Count()})"
            };
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
            };
            foreach (var entry in group)
            {
                var item = new MyListItem
                {
                    Title = entry.VersionName,
                    Height = 42d,
                    Type = MyListItem.CheckType.Clickable,
                    Logo = LauncherPaths.ImageRoot + "Blocks/NeoForge.png",
                    LogoScale = 0.9,
                    Info = entry.IsBeta
                        ? Lang.Text("Download.Version.Type.Preview")
                        : Lang.Text("Download.Version.Type.Stable")
                };
                var captured = entry;
                item.Click += (_, _) => SaveInstaller(
                    [$"{captured.UrlBase}-installer.jar"],
                    $"neoforge-{captured.ApiName}-installer.jar",
                    null);
                stack.Children.Add(item);
            }

            card.Children.Add(stack);
            _panMain.Children.Add(card);
        }
    }

    private void BuildSimpleList()
    {
        switch (_kind)
        {
            case DownloadLoaderKind.OptiFine:
            {
                foreach (var group in GameDownload
                             .DlOptiFineListLoader.output.Value
                             .GroupBy(entry => entry.Inherit ?? "")
                             .OrderByDescending(group => group.Key))
                {
                    var card = new MyCard
                    {
                        Margin = new Thickness(0d, 0d, 0d, 15d),
                        Title = $"{group.Key} ({group.Count()})"
                    };
                    var stack = new StackPanel
                    {
                        Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
                    };
                    foreach (var entry in group)
                    {
                        var item = new MyListItem
                        {
                            Title = entry.DisplayName ?? entry.NameFile ?? "",
                            Height = 42d,
                            Type = MyListItem.CheckType.Clickable,
                            Logo = LauncherPaths.ImageRoot + "Blocks/GrassPath.png",
                            LogoScale = 0.9,
                            Info = entry.IsPreview
                                ? Lang.Text("Download.Version.Type.Preview")
                                : Lang.Text("Download.Version.Type.Release")
                        };
                        var captured = entry;
                        item.Click += (_, _) => SaveOptiFine(captured);
                        stack.Children.Add(item);
                    }

                    card.Children.Add(stack);
                    _panMain.Children.Add(card);
                }

                break;
            }
            case DownloadLoaderKind.Cleanroom:
            {
                foreach (var group in GameDownload
                             .DlCleanroomListLoader.output.Value
                             .GroupBy(entry => entry.Inherit)
                             .OrderByDescending(group => group.Key))
                {
                    var card = new MyCard
                    {
                        Margin = new Thickness(0d, 0d, 0d, 15d),
                        Title = $"{group.Key} ({group.Count()})"
                    };
                    var stack = new StackPanel
                    {
                        Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
                    };
                    foreach (var entry in group)
                    {
                        var captured = entry;
                        var item = new MyListItem
                        {
                            Title = entry.VersionName,
                            Height = 42d,
                            Type = MyListItem.CheckType.Clickable,
                            Logo = LauncherPaths.ImageRoot + "Blocks/Cleanroom.png",
                            LogoScale = 0.9,
                            Info = entry.IsBeta
                                ? Lang.Text("Download.Version.Type.Preview")
                                : Lang.Text("Download.Version.Type.Stable")
                        };
                        item.Click += (_, _) => SaveInstaller(
                            [$"{captured.UrlBase}-installer.jar"],
                            $"cleanroom-{captured.ApiName}-installer.jar",
                            null);
                        stack.Children.Add(item);
                    }

                    card.Children.Add(stack);
                    _panMain.Children.Add(card);
                }

                break;
            }
            case DownloadLoaderKind.LiteLoader:
            {
                var card = new MyCard
                {
                    Margin = new Thickness(0d, 0d, 0d, 15d),
                    Title = Lang.Text(
                        "Download.Version.VersionListCount",
                        GameDownload.DlLiteLoaderListLoader.output.Value.Count)
                };
                var stack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
                };
                foreach (var entry in GameDownload.DlLiteLoaderListLoader.output.Value)
                {
                    var captured = entry;
                    var item = new MyListItem
                    {
                        Title = entry.Inherit ?? "",
                        Info = entry.FileName ?? "",
                        Height = 42d,
                        Type = MyListItem.CheckType.Clickable,
                        Logo = LauncherPaths.ImageRoot + "Blocks/Egg.png",
                        LogoScale = 0.9
                    };
                    item.Click += (_, _) => SaveInstaller(
                        GetLiteLoaderUrls(captured),
                        captured.FileName ?? "liteloader-installer.jar",
                        null);
                    stack.Children.Add(item);
                }

                card.Children.Add(stack);
                _panMain.Children.Add(card);
                break;
            }
            case DownloadLoaderKind.LabyMod:
                BuildLabyModList();
                break;
            case DownloadLoaderKind.Forge:
                BuildForgeMcList();
                break;
        }
    }

    private void BuildLabyModList()
    {
        var value = GameDownload.DlLabyModListLoader.output.Value;
        var card = new MyCard
        {
            Margin = new Thickness(0d, 0d, 0d, 15d),
            Title = Lang.Text("Download.Version.VersionList")
        };
        var stack = new StackPanel
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
        };
        foreach (var channel in new[]
                 {
                     "production",
                     "snapshot"
                 })
        {
            var channelObject = value?[channel] as JsonObject;
            if (channelObject is null)
                continue;
            var isSnapshot = channel == "snapshot";
            var item = new MyListItem
            {
                Title = channelObject["labyModVersion"]?.ToString() ?? channel,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Logo = LauncherPaths.ImageRoot + "Blocks/LabyMod.png",
                LogoScale = 0.9,
                Info = channel
            };
            item.Click += (_, _) => SaveInstaller(
                [
                    isSnapshot
                        ? "https://releases.labymod.net/api/v1/installer/snapshot/java"
                        : "https://releases.labymod.net/api/v1/installer/production/java"
                ],
                isSnapshot ? "LabyMod4SnapshotInstaller.jar" : "LabyMod4ProductionInstaller.jar",
                "Download.Version.Installer.LabyMod.Filter");
            stack.Children.Add(item);
        }

        card.Children.Add(stack);
        _panMain.Children.Add(card);
    }

    private void BuildForgeMcList()
    {
        var mcVersions = GameDownload.DlForgeListLoader.output.Value
            .OrderByDescending(
                version => version,
                Comparer<string>.Create(McVersionComparer.CompareVersion));
        foreach (var mcVersion in mcVersions)
        {
            var card = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title = mcVersion
            };
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                Tag = mcVersion
            };
            card.Children.Add(stack);
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                if (target.Tag is not string version)
                    return;
                LoadForgeVersions(target, version);
            };
            card.IsSwapped = true;
            _panMain.Children.Add(card);
        }
    }

    private void LoadForgeVersions(StackPanel target, string mcVersion)
    {
        target.Children.Clear();
        var loading = new MyLoading
        {
            Margin = new Thickness(5d),
            Text = Lang.Text("Download.Version.Forge.LoadingList")
        };
        var task = new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
            "DlForgeVersion " + mcVersion,
            GameDownload.DlForgeVersionMain);
        loading.State = task;
        task.OnStateChangedUi += (_, state, _) =>
        {
            switch (state)
            {
                case LoadState.Finished:
                    target.Children.Remove(loading);
                    FillForgeVersionList(target, task.output ?? []);
                    break;
                case LoadState.Failed:
                    target.Children.Remove(loading);
                    ShowForgeLoadError(target, task, mcVersion);
                    break;
                case LoadState.Aborted:
                    target.Children.Remove(loading);
                    break;
            }
        };
        target.Children.Add(loading);
        task.Start(mcVersion);
    }

    private void ShowForgeLoadError(
        StackPanel target,
        Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>> task,
        string mcVersion)
    {
        target.Children.Add(
            new TextBlock
            {
                Text = Lang.Text(
                    "Download.Install.State.GetVersionListFailed",
                    task.Error?.Message ?? Lang.Text("Download.Install.Error.OperationFailed")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6d, 4d, 6d, 8d)
            });
        var retryButton = new MyButton
        {
            Text = Lang.Text("Common.Action.Refresh"),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 90d,
            Padding = new Thickness(13d, 0d, 13d, 0d)
        };
        retryButton.Click += (_, _) => LoadForgeVersions(target, mcVersion);
        target.Children.Add(retryButton);
    }

    private static void FillForgeVersionList(
        StackPanel target,
        List<GameDownload.DlForgeVersionEntry> entries)
    {
        var versions = entries.OrderByDescending(entry => entry.version).ToList();
        if (versions.Count == 0)
        {
            target.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoAvailableVersion"),
                    Margin = new Thickness(6d, 4d, 6d, 8d)
                });
            return;
        }

        var latest = versions[0];
        var recommended = versions.LastOrDefault(entry => entry.IsRecommended);
        if (!ReferenceEquals(latest, recommended))
            AddForgeVersionRow(target, recommended, "Download.Version.Type.Recommended");
        AddForgeVersionRow(target, latest, "Download.Version.Latest.Title");
        target.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Version.AllVersions", versions.Count),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 13d, 0d, 4d)
            });
        foreach (var entry in versions)
            AddForgeVersionRow(target, entry, null);
    }

    private static void AddForgeVersionRow(
        StackPanel target,
        GameDownload.DlForgeVersionEntry? entry,
        string? typeKey)
    {
        if (entry is null)
            return;

        var infoParts = new List<string>();
        if (typeKey is not null)
            infoParts.Add(Lang.Text(typeKey));
        if (entry.ReleaseTime is not null)
            infoParts.Add(
                Lang.Text(
                    "Download.Version.ReleaseDate",
                    Lang.Date(entry.ReleaseTime.Value, "g")));
        if (typeKey is null && LauncherCommon.ModeDebug)
            infoParts.Add(Lang.Text("Download.Version.Forge.Type", entry.Category));

        var item = new MyListItem
        {
            Title = entry.FileVersion,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Logo = LauncherPaths.ImageRoot + "Blocks/Anvil.png",
            Info = string.Join("  |  ", infoParts)
        };
        var captured = entry;
        item.Click += (_, _) => SaveForgelike(captured);
        target.Children.Add(item);
    }

    private static void SaveForgelike(GameDownload.DlForgeVersionEntry entry)
    {
        var inherit = entry.Inherit.Replace("-", "_");
        var fileName =
            $"{inherit}-{entry.FileVersion}/forge-{inherit}-{entry.FileVersion}-{entry.Category}.{entry.FileExtension}";
        SaveInstaller(
            [
                $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{fileName}"
            ],
            $"forge-{entry.Inherit}-{entry.FileVersion}-{entry.Category}.{entry.FileExtension}",
            null);
    }

    private static void SaveOptiFine(GameDownload.DlOptiFineListEntry entry)
    {
        var inherit = entry.Inherit ?? "";
        var displayName = entry.DisplayName ?? "";
        var bmclapiInherit = inherit is "1.8" or "1.9" ? inherit + ".0" : inherit;
        var url = entry.IsPreview
            ? $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U_{displayName.Replace($"{inherit} ", "").Replace(" ", "/")}"
            : $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U/{displayName.Replace($"{inherit} ", "")}";
        SaveInstaller(
            [url, $"https://optifine.net/adloadx?f={entry.NameFile}"],
            entry.NameFile ?? $"OptiFine-{displayName}.jar",
            null);
    }

    private static string[] GetLiteLoaderUrls(GameDownload.DlLiteLoaderListEntry entry)
    {
        var inherit = entry.Inherit ?? "";
        if (!entry.IsLegacy)
            return
            [
                $"http://jenkins.liteloader.com/job/LiteLoaderInstaller%20{inherit}/lastSuccessfulBuild/artifact/"
                + $"{(inherit == "1.8" ? "ant/dist/" : "build/libs/")}{entry.FileName}"
            ];
        return inherit switch
        {
            "1.7.10" =>
                ["https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar"],
            "1.7.2" => ["https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar"],
            "1.6.4" => ["https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar"],
            "1.6.2" => ["https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar"],
            "1.5.2" => ["https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar"],
            _ => []
        };
    }

    private static void SaveInstaller(IEnumerable<string> urls, string fileName, string? filterKey)
    {
        try
        {
            var urlList = urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().ToList();
            if (urlList.Count == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Download.Error.OperationFailed")));
                return;
            }

            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Version.SelectSaveLocation"),
                fileName,
                filterKey is null ? "*.jar|*.jar" : Lang.Text(filterKey));
            if (string.IsNullOrEmpty(target))
                return;

            var loader = new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                [
                    new LoaderDownload(
                        Lang.Text("Minecraft.Download.Stage.DownloadMainFile"),
                        [new DownloadFile(urlList, target, new FileChecker(1024 * 64))])
                    {
                        ProgressWeight = 1d
                    }
                ])
            {
                OnStateChanged = instance => OnSaveStateChanged(instance)
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "开始加载器安装器下载失败");
            HintService.Show(Hints.Error(Lang.Text("Download.Error.OperationFailed")));
        }
    }

    private static void OnSaveStateChanged(Loading.LoaderBase loader)
    {
        switch (loader.State)
        {
            case LoadState.Finished:
                HintService.Show(
                    Hints.Success($"{loader.name}{Lang.Text("Common.Status.Success")}"));
                break;
            case LoadState.Failed:
                HintService.Show(
                    Hints.Error(
                        $"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error?.Message}"));
                break;
            case LoadState.Aborted:
                HintService.Show(
                    Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
        }
    }
}