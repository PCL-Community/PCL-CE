using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadLegacyFabric
{
    public PageDownloadLegacyFabric()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, GameDownload.DlLegacyFabricListLoader,
            _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            var versions =
                (JsonArray)GameDownload.DlLegacyFabricListLoader.output.Value["installer"];
            PanVersions.Children.Clear();
            foreach (var Version in versions)
                PanVersions.Children.Add(DownloadUi.LegacyFabricDownloadListItem(
                    (JsonObject)Version,
                    (a, b) => this.LegacyFabric_Selected((MyListItem)a, b)));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", versions.Count);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 LegacyFabric 版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
    }

    private void LegacyFabric_Selected(MyListItem sender, EventArgs e)
    {
        DownloadUi.McDownloadLegacyFabricLoaderSave((JsonObject)sender.Tag);
    }

    private void BtnWeb_Click(object sender, MouseButtonEventArgs e)
    {
        Common.OpenWebsite("https://legacyfabric.net/");
    }
}