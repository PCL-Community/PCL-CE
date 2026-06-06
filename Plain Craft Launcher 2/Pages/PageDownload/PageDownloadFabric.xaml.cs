using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadFabric
{
    public PageDownloadFabric()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, ModDownload.dlFabricListLoader, _ => Load_OnFinish());
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
            var versions = (JsonArray)ModDownload.dlFabricListLoader.output.Value["installer"];
            PanVersions.Children.Clear();
            foreach (var Version in versions)
                PanVersions.Children.Add(
                    ModDownloadLib.FabricDownloadListItem((JsonObject)Version,
                        (sender, e) => Fabric_Selected((MyListItem)sender, e)));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", versions.Count);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Fabric 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    private void Fabric_Selected(MyListItem sender, EventArgs e)
    {
        ModDownloadLib.McDownloadFabricLoaderSave((JsonObject)sender.Tag);
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://www.fabricmc.net");
    }
}