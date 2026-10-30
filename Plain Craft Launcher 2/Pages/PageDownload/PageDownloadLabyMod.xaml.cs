using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadLabyMod
{
    public PageDownloadLabyMod()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, GameDownload.DlLabyModListLoader,
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
            var versions = GameDownload.DlLabyModListLoader.output.Value;
            if (versions is null)
                return;
            var productionEntry = new JsonObject();
            productionEntry.Add("channel", "production");
            productionEntry.Add("version", versions["production"]["labyModVersion"].ToString());
            var snapshotEntry = new JsonObject();
            snapshotEntry.Add("channel", "snapshot");
            snapshotEntry.Add("version", versions["snapshot"]["labyModVersion"].ToString());
            PanVersions.Children.Clear();
            PanVersions.Children.Add(DownloadUi.LabyModDownloadListItem(productionEntry,
                (a, b) => this.LabyMod_Production_Selected((MyListItem)a, b)));
            PanVersions.Children.Add(DownloadUi.LabyModDownloadListItem(snapshotEntry,
                (a, b) => this.LabyMod_Snapshot_Selected((MyListItem)a, b)));
            LocalizedText.TryBind(
                CardVersions,
                "Title",
                () => Lang.Text("Download.Version.VersionListCount", versions.Count));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化 LabyMod 版本列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
    }

    private void LabyMod_Production_Selected(MyListItem sender, EventArgs e)
    {
        DownloadUi.McDownloadLabyModProductionLoaderSave();
    }

    private void LabyMod_Snapshot_Selected(MyListItem sender, EventArgs e)
    {
        DownloadUi.McDownloadLabyModSnapshotLoaderSave();
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        Common.OpenWebsite("https://labymod.net");
    }
}