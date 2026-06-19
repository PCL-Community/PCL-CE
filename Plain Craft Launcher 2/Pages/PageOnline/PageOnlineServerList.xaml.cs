// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageOnlineServerList : MyPageRight, IRefreshable
{
    private const int PageSize = PCL.Online.ModrinthServerCatalog.DefaultPageSize;
    private bool _hasLoaded;
    private bool _isRefreshing;
    private int _page;
    private int _totalHits;

    public PageOnlineServerList()
    {
        InitializeComponent();
        PanSearchBox.Search += (_, _) => StartNewSearch();
        PanSearchBox.KeyDown += EnterTrigger;
        TextSearchVersion.KeyDown += EnterTrigger;
        BtnSearchReset.Click += (_, _) =>
        {
            ResetFilter();
            StartNewSearch();
        };
        BtnPageFirst.Click += (_, _) => ChangePage(0);
        BtnPageLeft.Click += (_, _) => ChangePage(_page - 1);
        BtnPageRight.Click += (_, _) => ChangePage(_page + 1);
        PageEnter += () =>
        {
            if (!_hasLoaded)
                _ = RefreshAsync();
        };
    }

    public void Refresh()
    {
        _ = RefreshAsync();
    }

    private void StartNewSearch()
    {
        _page = 0;
        _ = RefreshAsync();
    }

    private void EnterTrigger(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            StartNewSearch();
    }

    private void ResetFilter()
    {
        PanSearchBox.Text = "";
        TextSearchVersion.Text = Lang.Text("Download.Comp.Filter.Version.Any");
        TextSearchVersion.SelectedIndex = 0;
        ComboSearchSort.SelectedIndex = 0;
    }

    private void ChangePage(int newPage)
    {
        if (newPage < 0 || _isRefreshing)
            return;

        _page = newPage;
        ScrollToTop();
        _ = RefreshAsync();
    }

    private void ScrollToTop()
    {
        if (PanBack.VerticalOffset > 0)
            PanBack.PerformVerticalOffsetDelta(-PanBack.VerticalOffset);
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;
        _hasLoaded = true;
        SetFilterEnabled(false);
        Load.Visibility = Visibility.Visible;
        LabEmpty.Visibility = Visibility.Collapsed;
        PanServers.Children.Clear();

        try
        {
            var result = await PCL.Online.ModrinthServerCatalog.SearchOnlineServersAsync(BuildSearchOptions());
            _totalHits = result.TotalHits;
            foreach (var server in result.Entries)
                PanServers.Children.Add(CreateServerItem(server));
            LabEmpty.Visibility = result.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdatePagination();
        }
        finally
        {
            Load.Visibility = Visibility.Collapsed;
            SetFilterEnabled(true);
            _isRefreshing = false;
        }
    }

    private PCL.Online.ModrinthServerSearchOptions BuildSearchOptions()
    {
        var gameVersion = TextSearchVersion.Text.Trim();
        if (gameVersion == Lang.Text("Download.Comp.Filter.Version.Any") ||
            gameVersion == Lang.Text("Download.Comp.Filter.Version.AllInputAvailable"))
            gameVersion = "";

        var sort = PCL.Online.ModrinthServerSort.Relevance;
        if (ComboSearchSort.SelectedItem is FrameworkElement { Tag: { } tag })
            sort = (PCL.Online.ModrinthServerSort)(int)ModBase.Val(tag);

        return new PCL.Online.ModrinthServerSearchOptions(
            PanSearchBox.Text,
            gameVersion,
            sort,
            _page * PageSize,
            PageSize);
    }

    private void SetFilterEnabled(bool enabled)
    {
        PanSearchBox.IsEnabled = enabled;
        TextSearchVersion.IsEnabled = enabled;
        ComboSearchSort.IsEnabled = enabled;
        BtnSearchReset.IsEnabled = enabled;
        CardPages.IsEnabled = enabled;
    }

    private void UpdatePagination()
    {
        CardPages.Visibility = _totalHits > PageSize || _page > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        LabPage.Text = Lang.Number(_page + 1, "N0");

        BtnPageFirst.IsEnabled = _page > 1;
        BtnPageFirst.Opacity = _page > 1 ? 1d : 0.2d;
        BtnPageLeft.IsEnabled = _page > 0;
        BtnPageLeft.Opacity = _page > 0 ? 1d : 0.2d;

        var hasNextPage = (_page + 1) * PageSize < _totalHits;
        BtnPageRight.IsEnabled = hasNextPage;
        BtnPageRight.Opacity = hasNextPage ? 1d : 0.2d;
    }

    private UIElement CreateServerItem(PCL.Online.ModrinthServerEntry server)
    {
        var root = new Grid
        {
            Height = 64,
            Margin = new Thickness(0, 0, 0, 8),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        root.SetResourceReference(Panel.BackgroundProperty, "ColorBrushSemiTransparent");

        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });

        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.1, GridUnitType.Star) });

        var fallbackIcon = "pack://application:,,,/images/Icons/NoIcon.png";
        var icon = new MyImage
        {
            Source = string.IsNullOrWhiteSpace(server.IconUrl) ? fallbackIcon : server.IconUrl,
            FallbackSource = fallbackIcon,
            Width = 50,
            Height = 50,
            CornerRadius = new CornerRadius(6),
            Stretch = Stretch.UniformToFill,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        Grid.SetColumn(icon, 1);
        Grid.SetRow(icon, 1);
        Grid.SetRowSpan(icon, 3);

        var title = new TextBlock
        {
            Text = server.Title,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = server.Title
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
        Grid.SetColumn(title, 3);
        Grid.SetRow(title, 1);

        var description = string.IsNullOrWhiteSpace(server.Description) ? server.Address : server.Description;
        var desc = new TextBlock
        {
            Text = description,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = description
        };
        desc.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray3");
        Grid.SetColumn(desc, 3);
        Grid.SetRow(desc, 2);

        var info = new TextBlock
        {
            Text = FormatServerInfo(server),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = server.Address
        };
        info.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4");
        Grid.SetColumn(info, 3);
        Grid.SetRow(info, 3);

        var enterButton = new MyIconButton
        {
            SvgIcon = "lucide/play",
            Height = 30,
            Width = 30,
            LogoScale = 0.85,
            ToolTip = Lang.Text("Online.ServerList.Enter")
        };
        enterButton.Click += (_, _) => EnterServer(server, enterButton);
        Grid.SetColumn(enterButton, 5);
        Grid.SetRow(enterButton, 1);
        Grid.SetRowSpan(enterButton, 3);

        root.Children.Add(icon);
        root.Children.Add(title);
        root.Children.Add(desc);
        root.Children.Add(info);
        root.Children.Add(enterButton);
        return root;
    }

    private static string FormatServerInfo(PCL.Online.ModrinthServerEntry server)
    {
        var version = server.Versions.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(version))
            version = Lang.Text("Common.State.Unknown");
        var online = server.PlayersMax > 0
            ? server.PlayersOnline.ToString()
            : Lang.Text("Common.State.Unknown");
        var max = server.PlayersMax > 0
            ? server.PlayersMax.ToString()
            : Lang.Text("Common.State.Unknown");
        return Lang.Text("Online.ServerList.CardInfo", version, online, max, server.Address);
    }

    private void EnterServer(PCL.Online.ModrinthServerEntry server, MyIconButton enterButton)
    {
        if (string.IsNullOrWhiteSpace(server.Address))
        {
            ModMain.Hint(Lang.Text("Online.ServerList.AddressMissing"), ModMain.HintType.Critical);
            return;
        }

        enterButton.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            var launchStarted = false;
            try
            {
                if (!EnsureInstanceListLoaded())
                    return;

                var instance = FindMatchingInstance(server.Versions);
                ModBase.RunInUiWait(() =>
                {
                    if (instance is null)
                    {
                        PromptMissingVersion(server);
                        return;
                    }

                    launchStarted = ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions
                    {
                        instance = instance,
                        ServerIp = server.Address
                    });
                    if (launchStarted)
                    {
                        ModMain.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.Launch });
                        ModMain.Hint(Lang.Text("Online.ServerList.Connecting", server.Title));
                    }
                    else
                    {
                        ModMain.Hint(Lang.Text("Online.ServerList.LaunchFailed"), ModMain.HintType.Critical);
                    }
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Online.ServerList.LaunchFailed"), ModBase.LogLevel.Feedback);
                ModBase.RunInUi(() => ModMain.Hint(Lang.Text("Online.ServerList.LaunchFailed"),
                    ModMain.HintType.Critical));
            }
            finally
            {
                if (!launchStarted)
                    ModBase.RunInUi(() => enterButton.IsEnabled = true);
            }
        }, "OnlineServerEnter");
    }

    private static bool EnsureInstanceListLoaded()
    {
        if (!EnsureMinecraftFolderSelected())
        {
            ModBase.RunInUi(() =>
            {
                ModMain.Hint(Lang.Text("Online.ServerList.FolderMissing"), ModMain.HintType.Critical);
                ModMain.frmMain.PageChange(FormMain.PageType.InstanceSelect);
            });
            return false;
        }

        if (ModInstanceList.mcInstanceListLoader.State == ModBase.LoadState.Finished &&
            string.Equals(ModInstanceList.mcInstanceListLoader.input, ModFolder.mcFolderSelected,
                StringComparison.OrdinalIgnoreCase))
            return true;

        ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
            ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\", true);
        return true;
    }

    private static bool EnsureMinecraftFolderSelected()
    {
        if (!string.IsNullOrWhiteSpace(ModFolder.mcFolderSelected) &&
            Directory.Exists(ModFolder.mcFolderSelected))
            return true;

        ModFolder.mcFolderListLoader.WaitForExit(isForceRestart: true);
        var folder = ModFolder.mcFolderList
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Location) &&
                                    Directory.Exists(item.Location));
        if (folder is null)
            return false;

        ModFolder.mcFolderSelected = folder.Location;
        States.Game.SelectedFolder = folder.Location.Replace(ModBase.exePath, "$");
        return true;
    }

    private static McInstance? FindMatchingInstance(IEnumerable<string> versions)
    {
        var versionSet = versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (versionSet.Count == 0)
            return ModInstanceList.McMcInstanceSelected;

        return ModInstanceList.mcInstanceList.Values
            .SelectMany(list => list)
            .Where(instance => instance.state != McInstanceState.Error)
            .FirstOrDefault(instance =>
            {
                try
                {
                    instance.Load();
                    return versionSet.Contains(instance.Info.VanillaName ?? "");
                }
                catch
                {
                    return false;
                }
            });
    }

    private static void PromptMissingVersion(PCL.Online.ModrinthServerEntry server)
    {
        var version = server.Versions.FirstOrDefault() ?? Lang.Text("Common.State.Unknown");
        var result = ModMain.MyMsgBox(
            Lang.Text("Online.ServerList.NoMatchingVersion.Message", server.Title, version),
            Lang.Text("Online.ServerList.NoMatchingVersion.Title"),
            Lang.Text("Online.ServerList.NoMatchingVersion.Download"),
            Lang.Text("Online.ServerList.NoMatchingVersion.Manual"),
            Lang.Text("Common.Action.Cancel"));
        switch (result)
        {
            case 1:
                PageDownloadInstall.mcVersionWaitingForSelect = version;
                ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
                break;
            case 2:
                PageSelectRight.InstanceSelectedCallback = instance => LaunchSelectedInstance(server, instance);
                ModMain.frmMain.PageChange(FormMain.PageType.Launch);
                ModMain.frmMain.PageChange(FormMain.PageType.InstanceSelect);
                ModMain.Hint(Lang.Text("Online.ServerList.NoMatchingVersion.SelectManually"));
                break;
        }
    }

    private static void LaunchSelectedInstance(PCL.Online.ModrinthServerEntry server, McInstance instance)
    {
        var launchStarted = ModLaunch.McLaunchStart(new ModLaunch.McLaunchOptions
        {
            instance = instance,
            ServerIp = server.Address
        });
        if (launchStarted)
        {
            ModMain.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.Launch });
            ModMain.Hint(Lang.Text("Online.ServerList.Connecting", server.Title));
        }
        else
        {
            ModMain.Hint(Lang.Text("Online.ServerList.LaunchFailed"), ModMain.HintType.Critical);
        }
    }
}
