using PCL.Core.App.Localization;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class PageDownloadClient
{
    private readonly PageDownloadClientViewModel _vm = new();

    public PageDownloadClient()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        DataContext = _vm;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanBack, null, GameDownload.dlClientListLoader,
            _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private void Load_OnFinish()
    {
        try
        {
            if (GameDownload.dlClientListLoader.output.Value?["versions"] is not JsonArray versions)
                return;

            var classified = PageDownloadClientViewModel.ClassifyVersions(versions);
            _vm.SetGroups(classified);
            RenderLatestVersionCard(classified);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 MC 版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     构建顶部的“最新版本”卡片。它只有一张卡、不属于列表，因此保持原有的代码构建方式。
    /// </summary>
    private void RenderLatestVersionCard(IReadOnlyDictionary<McVersionCategory, List<JsonObject>> classified)
    {
        PanLatest.Children.Clear();

        var release = classified[McVersionCategory.Release].FirstOrDefault();
        var snapshot = classified[McVersionCategory.Snapshot].FirstOrDefault();
        var topestVersions = new List<JsonObject>();
        if (release is not null)
            topestVersions.Add(CloneWithLore(release, "Download.Version.Latest.Release"));
        if (snapshot is not null &&
            (release is null ||
             McVersionClassifier.GetReleaseTime(release) < McVersionClassifier.GetReleaseTime(snapshot)))
            topestVersions.Add(CloneWithLore(snapshot, "Download.Version.Latest.Development"));
        if (topestVersions.Count == 0)
            return;

        var cardInfo = new MyCard { Margin = new Thickness(0d, 0d, 0d, 15d) };
        LocalizedText.TryBind(
            cardInfo,
            "Title",
            () => Lang.Text("Download.Version.Latest.Title"));
        var panInfo = new StackPanel
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(0d, 0d),
            Tag = topestVersions
        };
        MyCard.StackInstall(ref panInfo, InstallVersionItems);
        cardInfo.Children.Add(panInfo);
        PanLatest.Children.Add(cardInfo);
    }

    /// <summary>复制一份版本数据，并替换为“最新版本”卡片使用的说明文本。</summary>
    private static JsonObject CloneWithLore(JsonObject version, string loreKey)
    {
        var clone = (JsonObject)version.DeepClone();
        clone["lore"] = loreKey;
        clone["loreArgs"] = new JsonArray(McVersionClassifier.GetReleaseTime(clone).ToString("O"));
        clone["loreArgsType"] = "date";
        return clone;
    }

    /// <summary>装填“最新版本”卡片中的条目（该卡片不带折叠，构建时直接装填）。</summary>
    private static void InstallVersionItems(StackPanel stack)
    {
        if (stack.Tag is not IEnumerable entries)
            return;
        foreach (var item in entries)
            if (item is JsonObject version)
                stack.Children.Add(DownloadUi.McDownloadListItem(version, DownloadUi.McDownloadMenuSave, true));
    }

    /// <summary>点击版本条目，弹出下载菜单。</summary>
    private void VersionItem_Click(object sender, MouseButtonEventArgs e)
    {
        DownloadUi.McDownloadMenuSave(sender, e);
    }

    /// <summary>
    ///     列表项由 DataTemplate 生成，而 <see cref="MyListItem.ContentHandler" /> 不是依赖属性、无法绑定，
    ///     因此在首次进入可视树时做一次性代码映射（与收藏夹页的 FavoriteItem_Loaded 同一做法）。
    /// </summary>
    private void VersionItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is MyListItem item)
            DownloadUi.McDownloadListItemAttachMenu(item);
    }
}
