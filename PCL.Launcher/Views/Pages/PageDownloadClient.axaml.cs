using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL.Views.Pages;

// TODO: 版本点击后的安装/下载页（WPF DownloadUi 安装流程）尚未迁移，当前只抛出 VersionSelected 事件。
/// <summary>
///     下载页的 Minecraft 版本列表：按正式版、快照、旧版本、愚人节分类展示。
///     WPF 端点击版本后由 DownloadUi 打开安装页，迁移完成前通过 <see cref="VersionSelected" /> 交给上层接线。
/// </summary>
public partial class PageDownloadClient : PageBase
{
    private static readonly McVersionCategory[] CategoryOrder =
    [
        McVersionCategory.Release,
        McVersionCategory.Snapshot,
        McVersionCategory.BeforeRelease,
        McVersionCategory.AprilFools
    ];

    private readonly ScrollViewer? _panBack;
    private readonly StackPanel? _panMain;
    private readonly Border? _panLoad;
    private readonly TextBlock? _loadText;

    private bool _loaderInitialized;

    public PageDownloadClient()
    {
        InitializeComponent();
        _panBack = this.FindControl<ScrollViewer>("PanBack");
        _panMain = this.FindControl<StackPanel>("PanMain");
        _panLoad = this.FindControl<Border>("PanLoad");
        _loadText = this.FindControl<TextBlock>("LoadText");
    }

    /// <summary>用户选择了某个版本，参数为该版本的 JSON 项。</summary>
    public event Action<JsonObject>? VersionSelected;

    public override void OnNavigatedTo(object? parameter)
    {
        _panBack?.Offset = default;
        _InitializeLoader();
    }

    private void _InitializeLoader()
    {
        if (_loaderInitialized)
            return;
        _loaderInitialized = true;

        var loader = GameDownload.dlClientListLoader;
        loader.OnStateChangedUi += (_, _, _) => Dispatcher.UIThread.Post(_RefreshLoaderState);
        _RefreshLoaderState();
        if (loader.State == LoadState.Waiting)
            loader.Start();
    }

    private void _RefreshLoaderState()
    {
        var loader = GameDownload.dlClientListLoader;
        switch (loader.State)
        {
            case LoadState.Waiting:
            case LoadState.Loading:
                _panLoad?.IsVisible = true;
                _loadText?.Text = Lang.Text("Download.Version.LoadingList");
                break;

            case LoadState.Finished:
                _panLoad?.IsVisible = false;
                _BuildVersionList();
                break;

            case LoadState.Failed:
                _panLoad?.IsVisible = false;
                HintService.Show(Hints.Error(Lang.Text("Download.Error.OperationFailed")));
                break;

            case LoadState.Aborted:
                _panLoad?.IsVisible = false;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void _BuildVersionList()
    {
        if (_panMain is null)
            return;

        try
        {
            var result = GameDownload.dlClientListLoader.output;
            if (result.Value?["versions"] is not JsonArray versions)
                return;

            var dictionary = CategoryOrder.ToDictionary(
                category => category,
                _ => new List<JsonObject>());
            foreach (var node in versions)
            {
                if (node is not JsonObject version)
                    continue;
                var category = McVersionClassifier.ClassifyVersion(version);
                if (dictionary.TryGetValue(category, out var categoryList))
                    categoryList.Add(version);
            }

            foreach (var category in CategoryOrder)
                dictionary[category]
                    .Sort((left, right) => McVersionClassifier
                        .GetReleaseTime(right)
                        .CompareTo(McVersionClassifier.GetReleaseTime(left)));

            _panMain.Children.Clear();

            var latestVersions = _GetLatestVersions(dictionary);
            if (latestVersions.Count > 0)
                _panMain.Children.Add(
                    _CreateCard(
                        Lang.Text("Download.Version.Latest.Title"),
                        latestVersions,
                        true));

            foreach (var category in CategoryOrder)
            {
                var categoryVersions = dictionary[category];
                if (categoryVersions.Count == 0)
                    continue;
                _panMain.Children.Add(
                    _CreateCard(
                        $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} ({categoryVersions.Count})",
                        categoryVersions,
                        false));
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "可视化 MC 版本列表出错");
        }
    }

    private static List<JsonObject> _GetLatestVersions(
        IReadOnlyDictionary<McVersionCategory, List<JsonObject>> dictionary)
    {
        var result = new List<JsonObject>();
        var latestRelease = dictionary[McVersionCategory.Release].FirstOrDefault();
        if (latestRelease is not null)
            result.Add(latestRelease);

        var latestSnapshot = dictionary[McVersionCategory.Snapshot].FirstOrDefault();
        if (latestSnapshot is null)
            return result;
        if (latestRelease is null
            || McVersionClassifier.GetReleaseTime(latestRelease)
            < McVersionClassifier.GetReleaseTime(latestSnapshot))
            result.Add(latestSnapshot);
        return result;
    }

    private Control _CreateCard(string title, List<JsonObject> versions, bool showLore)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            Margin = new Thickness(0, 0, 0, 12)
        };
        titleBlock.Classes.Add("RowTitle");

        var buttonPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        foreach (var version in versions)
        {
            var id = version["id"]?.ToString() ?? "";
            var releaseTime = McVersionClassifier.GetReleaseTime(version);
            var tooltip = releaseTime == default ? id : Lang.Date(releaseTime, "g");
            if (showLore && version["lore"]?.ToString() is { Length: > 0 } loreKey)
                tooltip = $"{Lang.Text(loreKey)}\n{tooltip}";

            var button = new Button
            {
                Content = id,
                Margin = new Thickness(0, 0, 8, 8)
            };
            button.Classes.Add("PageLinkButton");
            ToolTip.SetTip(button, tooltip);
            var captured = version;
            button.Click += (_, _) => VersionSelected?.Invoke(captured);
            buttonPanel.Children.Add(button);
        }

        var panel = new StackPanel();
        panel.Children.Add(titleBlock);
        panel.Children.Add(buttonPanel);

        var card = new Border
        {
            Child = panel,
            Padding = new Thickness(18, 16),
            Margin = new Thickness(0, 0, 0, 15)
        };
        card.Classes.Add("Card");
        return card;
    }
}