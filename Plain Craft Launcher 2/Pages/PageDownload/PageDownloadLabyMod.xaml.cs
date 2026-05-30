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
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, ModDownload.DlLabyModListLoader, _ => Load_OnFinish());
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
            var Versions = ModDownload.DlLabyModListLoader.Output.Value;
            if (Versions is null)
                return;
            var ProductionEntry = new JsonObject();
            ProductionEntry.Add("channel", "production");
            ProductionEntry.Add("version", Versions["production"]["labyModVersion"].ToString());
            var SnapshotEntry = new JsonObject();
            SnapshotEntry.Add("channel", "snapshot");
            SnapshotEntry.Add("version", Versions["snapshot"]["labyModVersion"].ToString());
            PanVersions.Children.Clear();
            PanVersions.Children.Add(ModDownloadLib.LabyModDownloadListItem(ProductionEntry,
                (a, b) => this.LabyMod_Production_Selected((MyListItem)a, b)));
            PanVersions.Children.Add(ModDownloadLib.LabyModDownloadListItem(SnapshotEntry,
                (a, b) => this.LabyMod_Snapshot_Selected((MyListItem)a, b)));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", Versions.Count);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 LabyMod 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    private void LabyMod_Production_Selected(MyListItem sender, EventArgs e)
    {
        ModDownloadLib.McDownloadLabyModProductionLoaderSave();
    }

    private void LabyMod_Snapshot_Selected(MyListItem sender, EventArgs e)
    {
        ModDownloadLib.McDownloadLabyModSnapshotLoaderSave();
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://labymod.net");
    }
}