using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
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
            DownloadLoaderKind.Cleanroom => "https://cleanroommc.com/zh/",
            DownloadLoaderKind.Fabric => "https://www.fabricmc.net",
            DownloadLoaderKind.LegacyFabric => "https://legacyfabric.net/",
            DownloadLoaderKind.LabyMod => "https://labymod.net",
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

    public static bool UsesVersionCard(DownloadLoaderKind kind)
    {
        return kind is DownloadLoaderKind.Fabric
            or DownloadLoaderKind.LegacyFabric
            or DownloadLoaderKind.LabyMod;
    }
}

/// <summary>
///     加载器下载子页：展示简介与官方版本列表，点击版本可保存对应的安装器 Jar。
///     结构按 WPF 页面区分：Fabric / Legacy Fabric / LabyMod 使用单一版本卡片，
///     Forge / NeoForge / Cleanroom / OptiFine / LiteLoader 使用按 MC 版本分组的可折叠卡片。
/// </summary>
internal sealed class PageDownloadLoaderPage : MyPageRight, IPageView, IRefreshable
{
    private readonly MyButton _btnWebsite;
    private readonly MyCard _cardTip;
    private readonly MyCard _cardVersions;
    private readonly Dictionary<string, List<GameDownload.DlForgeVersionEntry>> _forgeCache = [];
    private readonly DownloadLoaderKind _kind;
    private readonly TextBlock _labIntro;
    private readonly MyLoading _load;
    private readonly MyScrollViewer _panBack;
    private readonly MyCard _panLoad;
    private readonly StackPanel _panMain;
    private readonly StackPanel _panVersions;
    private bool _finished;

    public PageDownloadLoaderPage(DownloadLoaderKind kind)
    {
        _kind = kind;

        _labIntro = new TextBlock
        {
            Text = Lang.Text(DownloadLoaderMeta.GetDescriptionKey(kind)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 0d, 0d, 11d)
        };
        _btnWebsite = new MyButton
        {
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d, 13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = Lang.Text("Download.Version.OpenWebsite"),
            ColorType = MyButton.ColorState.Highlight
        };
        var website = DownloadLoaderMeta.GetWebsite(kind);
        _btnWebsite.Click += (_, _) => LauncherUi.Interaction?.OpenWebsite(website);

        var buttonGrid = new Grid
        {
            Height = 35d,
            ColumnDefinitions = new ColumnDefinitions("Auto")
        };
        buttonGrid.ColumnDefinitions[0].SharedSizeGroup = "Button";
        buttonGrid.Children.Add(_btnWebsite);

        var tipStack = new StackPanel
        {
            Margin = new Thickness(25d, 40d, 15d, 20d)
        };
        tipStack.Children.Add(_labIntro);
        tipStack.Children.Add(buttonGrid);

        _cardTip = new MyCard
        {
            Margin = new Thickness(20d, 20d, 20d, 0d),
            Title = Lang.Text(DownloadLoaderMeta.GetTitleKey(kind))
        };
        _cardTip.Children.Add(tipStack);
        Grid.SetIsSharedSizeScope(_cardTip, true);

        _panVersions = new StackPanel
        {
            Margin = new Thickness(20d, 40d, 18d, 20d)
        };
        _cardVersions = new MyCard
        {
            Margin = new Thickness(20d, 15d, 20d, 20d),
            Title = Lang.Text("Download.Version.VersionList")
        };
        _cardVersions.Children.Add(_panVersions);

        _panMain = new StackPanel
        {
            Margin = new Thickness(20d, 15d, 20d, 5d)
        };
        Grid.SetIsSharedSizeScope(_panMain, true);

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
            Margin = new Thickness(40d, 50d, 40d, 50d),
            UseAnimation = false
        };
        _panLoad.Children.Add(_load);

        var stack = new StackPanel
        {
            Margin = new Thickness(5d)
        };
        stack.Children.Add(_cardTip);
        Control content;
        if (DownloadLoaderMeta.UsesVersionCard(kind))
        {
            stack.Children.Add(_cardVersions);
            content = _cardVersions;
        }
        else
        {
            stack.Children.Add(_panMain);
            content = _panMain;
        }

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
            content,
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
        _labIntro.Text = Lang.Text(DownloadLoaderMeta.GetDescriptionKey(_kind));
        _btnWebsite.Text = Lang.Text("Download.Version.OpenWebsite");
        if (_finished)
            OnLoadFinished();
    }

    private void OnLoadFinished()
    {
        _finished = true;
        try
        {
            if (DownloadLoaderMeta.UsesVersionCard(_kind))
            {
                _panVersions.Children.Clear();
                switch (_kind)
                {
                    case DownloadLoaderKind.Fabric:
                        BuildFabricVersions(false);
                        break;
                    case DownloadLoaderKind.LegacyFabric:
                        BuildFabricVersions(true);
                        break;
                    case DownloadLoaderKind.LabyMod:
                        BuildLabyModVersions();
                        break;
                    case DownloadLoaderKind.OptiFine:
                    case DownloadLoaderKind.Forge:
                    case DownloadLoaderKind.NeoForge:
                    case DownloadLoaderKind.Cleanroom:
                    case DownloadLoaderKind.LiteLoader:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return;
            }

            _panMain.Children.Clear();
            _forgeCache.Clear();
            switch (_kind)
            {
                case DownloadLoaderKind.Forge:
                    BuildForgeMcList();
                    break;
                case DownloadLoaderKind.NeoForge:
                    BuildForgelikeGroups(
                        GameDownload.DlNeoForgeListLoader.output.Value,
                        entry => entry.Inherit,
                        BuildNeoForgeRows);
                    break;
                case DownloadLoaderKind.Cleanroom:
                    BuildForgelikeGroups(
                        GameDownload.DlCleanroomListLoader.output.Value,
                        entry => entry.Inherit,
                        BuildCleanroomRows);
                    break;
                case DownloadLoaderKind.OptiFine:
                    BuildOptiFineGroups();
                    break;
                case DownloadLoaderKind.LiteLoader:
                    BuildLiteLoaderGroups();
                    break;
                case DownloadLoaderKind.Fabric:
                case DownloadLoaderKind.LegacyFabric:
                case DownloadLoaderKind.LabyMod:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "可视化加载器版本列表出错");
            HintService.Show(Hints.Error(Lang.Text("Download.Error.OperationFailed")));
        }
    }

    #region Fabric / Legacy Fabric

    private void BuildFabricVersions(bool isLegacy)
    {
        var result = isLegacy
            ? GameDownload.DlLegacyFabricListLoader.output.Value
            : GameDownload.DlFabricListLoader.output.Value;
        if (result?["installer"] is not JsonArray installers)
            return;

        _cardVersions.Title = Lang.Text(
            "Download.Version.VersionListCount",
            installers.Count);
        foreach (var node in installers)
        {
            if (node is not JsonObject entry)
                continue;
            var rawVersion = entry["version"]?.ToString() ?? string.Empty;
            var title = isLegacy ? rawVersion : rawVersion.Replace("+build", "");
            var isStable = entry["stable"]?.GetValue<bool>() == true;
            var item = new MyListItem
            {
                Title = title,
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Logo = $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
                Info = Lang.Text(
                    isStable
                        ? "Download.Version.Type.Stable"
                        : "Download.Version.Type.Preview")
            };
            item.Click += (_, _) => SaveInstaller(
                [entry["url"]?.ToString() ?? ""],
                LauncherCommon.GetFileNameFromPath(entry["url"]?.ToString() ?? title),
                isLegacy
                    ? "Download.Version.Installer.LegacyFabric.Filter"
                    : "Download.Version.Installer.Fabric.Filter");
            if (!isLegacy)
            {
                var infoButton = new MyIconButton
                {
                    SvgIcon = "lucide/info",
                    LogoScale = 1.05d,
                    Theme = MyIconButton.Themes.Color
                };
                ToolTip.SetTip(infoButton, Lang.Text("Download.Version.Changelog"));
                infoButton.Click += (_, _) =>
                    LauncherUi.Interaction?.OpenWebsite("https://fabricmc.net/blog");
                item.Buttons = [infoButton];
            }

            _panVersions.Children.Add(item);
        }
    }

    #endregion

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

    #region LabyMod

    private void BuildLabyModVersions()
    {
        var value = GameDownload.DlLabyModListLoader.output.Value;
        if (value is null)
            return;

        _cardVersions.Title = Lang.Text("Download.Version.VersionListCount", value.Count);
        foreach (var channel in new[]
                 {
                     "production",
                     "snapshot"
                 })
        {
            if (value[channel] is not JsonObject channelObject)
                continue;
            var version = channelObject["labyModVersion"]?.ToString() ?? channel;
            var isSnapshot = channel == "snapshot";
            var item = new MyListItem
            {
                Title = $"{version} {Lang.Text(
                    isSnapshot
                        ? "Download.Version.Type.Snapshot"
                        : "Download.Version.Type.Stable")}",
                Info = Lang.Text(
                    isSnapshot
                        ? "Download.Version.Type.Snapshot"
                        : "Download.Version.Type.Stable"),
                Height = 42d,
                Type = MyListItem.CheckType.Clickable,
                Logo = $"{LauncherPaths.ImageRoot}Blocks/LabyMod.png"
            };

            var saveButton = new MyIconButton
            {
                SvgIcon = "lucide/save",
                Theme = MyIconButton.Themes.Color
            };
            ToolTip.SetTip(saveButton, Lang.Text("Download.Version.SaveAs"));
            saveButton.Click += (_, _) => SaveLabyMod(isSnapshot);

            var changelogButton = new MyIconButton
            {
                SvgIcon = "lucide/info",
                LogoScale = 1.05d,
                Theme = MyIconButton.Themes.Color
            };
            ToolTip.SetTip(changelogButton, Lang.Text("Download.Version.Changelog"));
            changelogButton.Click += (_, _) =>
                LauncherUi.Interaction?.OpenWebsite("https://www.labymod.net/zh_Hans/download");
            item.Buttons = [saveButton, changelogButton];
            item.Click += (_, _) => SaveLabyMod(isSnapshot);
            _panVersions.Children.Add(item);
        }
    }

    private static void SaveLabyMod(bool isSnapshot)
    {
        SaveInstaller(
            [
                isSnapshot
                    ? "https://releases.labymod.net/api/v1/installer/snapshot/java"
                    : "https://releases.labymod.net/api/v1/installer/production/java"
            ],
            isSnapshot ? "LabyMod4SnapshotInstaller.jar" : "LabyMod4ProductionInstaller.jar",
            "Download.Version.Installer.LabyMod.Filter");
    }

    #endregion

    #region Forge

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
                Title = mcVersion.Replace("_p", " P")
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
                if (target.Tag is string version)
                    LoadForgeVersions(card, target, version);
            };
            card.IsSwapped = true;
            _panMain.Children.Add(card);
        }
    }

    private void LoadForgeVersions(MyCard card, StackPanel target, string mcVersion)
    {
        target.Children.Clear();
        if (_forgeCache.TryGetValue(mcVersion, out var cached))
        {
            FillForgeVersionList(card, target, mcVersion, cached);
            return;
        }

        var loading = new MyLoading
        {
            Margin = new Thickness(5d),
            Text = Lang.Text("Download.Version.Forge.LoadingList")
        };
        var task = new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
            $"DlForgeVersion {mcVersion}",
            GameDownload.DlForgeVersionMain);
        loading.State = task;
        loading.Click += (_, _) =>
        {
            if (task.State == LoadState.Failed)
                task.Start(mcVersion, true);
        };
        task.OnStateChangedUi += (_, state, _) =>
        {
            switch (state)
            {
                case LoadState.Finished:
                    target.Children.Remove(loading);
                    var versions = task.output ?? [];
                    _forgeCache[mcVersion] = versions;
                    FillForgeVersionList(card, target, mcVersion, versions);
                    break;
                case LoadState.Failed:
                    // 保持错误态的加载环，点击即可重试。
                    break;
                case LoadState.Aborted:
                    target.Children.Remove(loading);
                    break;
            }
        };
        target.Children.Add(loading);
        task.Start(mcVersion);
    }

    private void FillForgeVersionList(
        MyCard card,
        StackPanel target,
        string mcVersion,
        List<GameDownload.DlForgeVersionEntry> entries)
    {
        target.Children.Clear();
        var versions = entries
            .OrderByDescending(entry => entry.version)
            .ToList();
        if (versions.Count == 0)
        {
            target.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoAvailableVersion"),
                    Margin = new Thickness(6d, 4d, 6d, 8d)
                });
            card.InstallMethod = stack => FillForgeVersionList(
                card,
                stack,
                mcVersion,
                _forgeCache.GetValueOrDefault(mcVersion, []));
            return;
        }

        if (versions.Count == 1)
        {
            AddForgeVersionRow(target, versions[0], null);
            card.InstallMethod = stack => FillForgeVersionList(
                card,
                stack,
                mcVersion,
                _forgeCache.GetValueOrDefault(mcVersion, []));
            return;
        }

        var latest = versions[0];
        var recommended = versions.LastOrDefault(entry => entry.IsRecommended);
        if (!ReferenceEquals(latest, recommended))
            AddForgeVersionRow(
                target,
                recommended,
                "Download.Version.Type.Recommended");
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

        card.InstallMethod = stack => FillForgeVersionList(
            card,
            stack,
            mcVersion,
            _forgeCache.GetValueOrDefault(mcVersion, []));
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
        {
            infoParts.Add(
                $"{Lang.Text(typeKey)}  |  {Lang.Text("Download.Version.Type.Stable")}");
        }
        else
        {
            if (entry.ReleaseTime is not null)
                infoParts.Add(
                    Lang.Text(
                        "Download.Version.ReleaseDate",
                        Lang.Date(entry.ReleaseTime.Value, "g")));
            if (LauncherCommon.ModeDebug)
                infoParts.Add(Lang.Text("Download.Version.Forge.Type", entry.Category));
        }

        var item = new MyListItem
        {
            Title = entry.VersionName,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Logo = $"{LauncherPaths.ImageRoot}Blocks/Anvil.png",
            Info = string.Join("  |  ", infoParts)
        };

        var changelogButton = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 1.05d,
            Theme = MyIconButton.Themes.Color
        };
        ToolTip.SetTip(changelogButton, Lang.Text("Download.Version.Changelog"));
        changelogButton.Click += (_, _) => LauncherUi.Interaction?.OpenWebsite(
            $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{entry.Inherit}-{entry.VersionName}/forge-{entry.Inherit}-{entry.VersionName}-changelog.txt");
        item.Buttons = [changelogButton];
        item.Click += (_, _) => SaveForgelike(entry);
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

    #endregion

    #region NeoForge / Cleanroom

    private void BuildForgelikeGroups<T>(
        List<T>? entries,
        Func<T, string> inheritSelector,
        Action<StackPanel, List<T>> fill)
        where T : class
    {
        if (entries is null)
            return;

        foreach (var group in entries
                     .GroupBy(inheritSelector)
                     .OrderByDescending(group => group.Key))
        {
            var items = group.ToList();
            if (items.Count == 0)
                continue;

            var card = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title = $"{group.Key} ({items.Count})"
            };
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                Tag = items
            };
            card.Children.Add(stack);
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                if (target.Tag is List<T> list)
                    fill(target, list);
            };
            card.IsSwapped = true;
            _panMain.Children.Add(card);
        }
    }

    private static void BuildNeoForgeRows(
        StackPanel target,
        List<GameDownload.DlNeoForgeListEntry> entries)
    {
        target.Children.Clear();
        if (entries.Count == 1)
        {
            AddForgelikeRow(
                target,
                entries[0],
                null,
                entries[0].IsBeta
                    ? "Download.Version.Type.Preview"
                    : "Download.Version.Type.Stable");
            return;
        }

        GameDownload.DlNeoForgeListEntry? latestStable = null;
        GameDownload.DlNeoForgeListEntry? latestBeta = null;
        foreach (var entry in entries)
            if (entry.IsBeta)
            {
                latestBeta ??= entry;
            }
            else
            {
                latestStable = entry;
                break;
            }

        if (latestStable is not null)
            AddForgelikeRow(
                target,
                latestStable,
                "Download.Version.Fresh.Latest",
                "Download.Version.Type.Stable");
        if (latestBeta is not null)
            AddForgelikeRow(
                target,
                latestBeta,
                "Download.Version.Fresh.Latest",
                "Download.Version.Type.Preview");

        AddAllVersionsLabel(target, entries.Count);
        foreach (var entry in entries)
            AddForgelikeRow(
                target,
                entry,
                null,
                entry.IsBeta
                    ? "Download.Version.Type.Preview"
                    : "Download.Version.Type.Stable");
    }

    private static void BuildCleanroomRows(
        StackPanel target,
        List<GameDownload.DlCleanroomListEntry> entries)
    {
        target.Children.Clear();
        if (entries.Count > 0)
            AddForgelikeRow(
                target,
                entries[0],
                "Download.Version.Fresh.Latest",
                "Download.Version.Type.Preview");

        AddAllVersionsLabel(target, entries.Count);
        foreach (var entry in entries)
            AddForgelikeRow(
                target,
                entry,
                null,
                entry.IsBeta
                    ? "Download.Version.Type.Preview"
                    : "Download.Version.Type.Stable");
    }

    private static void AddAllVersionsLabel(StackPanel target, int count)
    {
        target.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Version.AllVersions", count),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 13d, 0d, 4d)
            });
    }

    private static void AddForgelikeRow(
        StackPanel target,
        GameDownload.DlForgelikeEntry entry,
        string? preloadKey,
        string typeKey)
    {
        var infoParts = new List<string>();
        if (preloadKey is not null)
            infoParts.Add(Lang.Text(preloadKey));
        infoParts.Add(Lang.Text(typeKey));

        var item = new MyListItem
        {
            Title = entry.VersionName,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Logo = entry is GameDownload.DlCleanroomListEntry
                ? $"{LauncherPaths.ImageRoot}Blocks/Cleanroom.png"
                : $"{LauncherPaths.ImageRoot}Blocks/NeoForge.png",
            Info = string.Join("  |  ", infoParts)
        };
        var changelogButton = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 1.05d,
            Theme = MyIconButton.Themes.Color
        };
        ToolTip.SetTip(changelogButton, Lang.Text("Download.Version.Changelog"));
        changelogButton.Click += (_, _) =>
        {
            var url = GetForgelikeChangelogUrl(entry);
            if (!string.IsNullOrEmpty(url))
                LauncherUi.Interaction?.OpenWebsite(url);
        };
        item.Buttons = [changelogButton];
        item.Click += (_, _) => SaveForgelikeEntry(entry);
        target.Children.Add(item);
    }

    private static string GetForgelikeChangelogUrl(GameDownload.DlForgelikeEntry entry)
    {
        return entry switch
        {
            GameDownload.DlNeoForgeListEntry neo => $"{neo.UrlBase}-changelog.txt",
            GameDownload.DlCleanroomListEntry cleanroom => $"{cleanroom.UrlBase}-changelog.txt",
            _ => string.Empty
        };
    }

    private static void SaveForgelikeEntry(GameDownload.DlForgelikeEntry entry)
    {
        switch (entry)
        {
            case GameDownload.DlNeoForgeListEntry neo:
                SaveInstaller(
                    [
                        $"{neo.UrlBase.Replace(
                            "maven.neoforged.net/releases",
                            "bmclapi2.bangbang93.com/maven")}-installer.jar",
                        $"{neo.UrlBase}-installer.jar"
                    ],
                    $"neoforge-{neo.ApiName}-installer.jar",
                    null);
                break;
            case GameDownload.DlCleanroomListEntry cleanroom:
                SaveInstaller(
                    [$"{cleanroom.UrlBase}-installer.jar"],
                    $"cleanroom-{cleanroom.ApiName}-installer.jar",
                    null);
                break;
        }
    }

    #endregion

    #region OptiFine

    private void BuildOptiFineGroups()
    {
        const string snapshotKey = "Snapshot";
        var groups = new List<KeyValuePair<string, List<GameDownload.DlOptiFineListEntry>>>
        {
            new(snapshotKey, [])
        };
        for (var versionCode = 50; versionCode >= 0; versionCode--)
            groups.Add(
                new KeyValuePair<string, List<GameDownload.DlOptiFineListEntry>>(
                    $"1.{versionCode}",
                    []));

        var dictionary = groups.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var entry in GameDownload.DlOptiFineListLoader.output.Value)
            if (entry.Inherit?.StartsWith("1.") == true)
            {
                var parts = (entry.DisplayName ?? string.Empty).Split('.');
                var mainVersion = parts.Length > 1
                    ? $"1.{parts[1].Split(' ')[0]}"
                    : snapshotKey;
                if (dictionary.TryGetValue(mainVersion, out var list))
                    list.Add(entry);
                else
                    dictionary[snapshotKey].Add(entry);
            }
            else
            {
                dictionary[snapshotKey].Add(entry);
            }

        foreach (var (key, entries) in dictionary)
        {
            if (entries.Count == 0)
                continue;
            var title = key == snapshotKey
                ? Lang.Text("Download.Version.Optifine.Snapshot")
                : key;
            var card = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title = $"{title} ({entries.Count})"
            };
            var sorted = entries
                .OrderByDescending(
                    entry => entry.DisplayName,
                    Comparer<string?>.Create(McVersionComparer.CompareVersion))
                .ToList();
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                Tag = sorted
            };
            card.Children.Add(stack);
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                if (target.Tag is not List<GameDownload.DlOptiFineListEntry> list)
                    return;
                target.Children.Clear();
                foreach (var entry in list)
                    AddOptiFineRow(target, entry);
            };
            card.IsSwapped = true;
            _panMain.Children.Add(card);
        }
    }

    private static void AddOptiFineRow(
        StackPanel target,
        GameDownload.DlOptiFineListEntry entry)
    {
        var infoParts = new List<string>
        {
            Lang.Text(
                entry.IsPreview
                    ? "Download.Version.Type.Preview"
                    : "Download.Version.Type.Release")
        };
        if (entry.ReleaseTime is not null)
            infoParts.Add(
                Lang.Text(
                    "Download.Version.ReleaseDate",
                    Lang.Date(entry.ReleaseTime.Value, "d")));
        if (entry.RequiredForgeVersion is null)
            infoParts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
        else if (!string.IsNullOrEmpty(entry.RequiredForgeVersion))
            infoParts.Add(
                Lang.Text(
                    "Download.Version.Optifine.CompatibleForge",
                    entry.RequiredForgeVersion));

        var item = new MyListItem
        {
            Title = entry.DisplayName ?? string.Empty,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Logo = $"{LauncherPaths.ImageRoot}Blocks/GrassPath.png",
            Info = string.Join("  |  ", infoParts)
        };
        var changelogButton = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 1.05d,
            Theme = MyIconButton.Themes.Color
        };
        ToolTip.SetTip(changelogButton, Lang.Text("Download.Version.Changelog"));
        changelogButton.Click += (_, _) => LauncherUi.Interaction?.OpenWebsite(
            $"https://optifine.net/changelog?f={entry.NameFile}");
        item.Buttons = [changelogButton];
        item.Click += (_, _) => SaveOptiFine(entry);
        target.Children.Add(item);
    }

    private static void SaveOptiFine(GameDownload.DlOptiFineListEntry entry)
    {
        var inherit = entry.Inherit ?? string.Empty;
        var displayName = entry.DisplayName ?? string.Empty;
        var bmclapiInherit = inherit is "1.8" or "1.9" ? $"{inherit}.0" : inherit;
        var url = entry.IsPreview
            ? $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U_{displayName.Replace($"{inherit} ", "").Replace(" ", "/")}"
            : $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U/{displayName.Replace($"{inherit} ", "")}";
        SaveInstaller(
            [url, $"https://optifine.net/adloadx?f={entry.NameFile}"],
            entry.NameFile ?? $"OptiFine-{displayName}.jar",
            null);
    }

    #endregion

    #region LiteLoader

    private void BuildLiteLoaderGroups()
    {
        const string unknownKey = "Unknown";
        var dict = new Dictionary<string, List<GameDownload.DlLiteLoaderListEntry>>();
        for (var versionCode = 30; versionCode >= 0; versionCode--)
            dict.Add($"1.{versionCode}", []);
        dict.Add(unknownKey, []);

        foreach (var entry in GameDownload.DlLiteLoaderListLoader.output.Value)
        {
            var inherit = entry.Inherit ?? string.Empty;
            var segments = inherit.Split('.');
            var mainVersion = segments.Length > 1 ? $"1.{segments[1]}" : unknownKey;
            if (dict.TryGetValue(mainVersion, out var list))
                list.Add(entry);
            else
                dict[unknownKey].Add(entry);
        }

        foreach (var (key, entries) in dict)
        {
            if (entries.Count == 0)
                continue;
            var title = key == unknownKey
                ? Lang.Text("Minecraft.Version.Unknown")
                : key;
            var card = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title = $"{title} ({entries.Count})"
            };
            var sorted = entries
                .OrderByDescending(
                    entry => entry.Inherit,
                    Comparer<string?>.Create(McVersionComparer.CompareVersion))
                .ToList();
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                Tag = sorted
            };
            card.Children.Add(stack);
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                if (target.Tag is not List<GameDownload.DlLiteLoaderListEntry> list)
                    return;
                target.Children.Clear();
                foreach (var entry in list)
                    AddLiteLoaderRow(target, entry);
            };
            card.IsSwapped = true;
            _panMain.Children.Add(card);
        }
    }

    private static void AddLiteLoaderRow(
        StackPanel target,
        GameDownload.DlLiteLoaderListEntry entry)
    {
        var infoParts = new List<string>
        {
            Lang.Text(
                entry.IsPreview
                    ? "Download.Version.Type.Preview"
                    : "Download.Version.Type.Stable")
        };
        if (entry.ReleaseTime is not null)
            infoParts.Add(
                Lang.Text(
                    "Download.Version.ReleaseDate",
                    Lang.Date(entry.ReleaseTime.Value, "g")));

        var item = new MyListItem
        {
            Title = entry.Inherit ?? string.Empty,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Logo = $"{LauncherPaths.ImageRoot}Blocks/Egg.png",
            Info = string.Join("  |  ", infoParts)
        };

        if (entry.IsLegacy)
        {
            item.Buttons = [];
        }
        else
        {
            var listButton = new MyIconButton
            {
                SvgIcon = "lucide/list",
                Theme = MyIconButton.Themes.Color
            };
            ToolTip.SetTip(listButton, Lang.Text("Download.Version.ViewAllVersions"));
            listButton.Click += (_, _) => LauncherUi.Interaction?.OpenWebsite(
                $"https://jenkins.liteloader.com/view/{entry.Inherit}");
            item.Buttons = [listButton];
        }

        item.Click += (_, _) => SaveLiteLoader(entry);
        target.Children.Add(item);
    }

    private static void SaveLiteLoader(GameDownload.DlLiteLoaderListEntry entry)
    {
        SaveInstaller(
            GetLiteLoaderUrls(entry),
            entry.FileName ?? "liteloader-installer.jar",
            null);
    }

    private static string[] GetLiteLoaderUrls(GameDownload.DlLiteLoaderListEntry entry)
    {
        var inherit = entry.Inherit ?? "";
        if (!entry.IsLegacy)
            return
            [
                $"http://jenkins.liteloader.com/job/LiteLoaderInstaller%20{inherit}/lastSuccessfulBuild/artifact/{(inherit == "1.8" ? "ant/dist/" : "build/libs/")}{entry.FileName}"
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

    #endregion
}