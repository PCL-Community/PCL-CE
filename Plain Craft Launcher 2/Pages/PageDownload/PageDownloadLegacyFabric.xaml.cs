using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadLegacyFabric
{
    public PageDownloadLegacyFabric()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, ModDownload.DlLegacyFabricListLoader,
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
            var Versions = (JsonArray)ModDownload.DlLegacyFabricListLoader.Output.Value["installer"];
            PanVersions.Children.Clear();
            foreach (var Version in Versions)
                PanVersions.Children.Add(ModDownloadLib.LegacyFabricDownloadListItem((JsonObject)Version,
                    (a, b) => this.LegacyFabric_Selected((MyListItem)a, b)));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", Versions.Count);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 LegacyFabric 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    private void LegacyFabric_Selected(MyListItem sender, EventArgs e)
    {
        ModDownloadLib.McDownloadLegacyFabricLoaderSave((JsonObject)sender.Tag);
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://legacyfabric.net/");
    }
}