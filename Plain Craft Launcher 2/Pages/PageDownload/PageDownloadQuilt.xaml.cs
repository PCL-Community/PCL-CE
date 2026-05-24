using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadQuilt
{
    public PageDownloadQuilt()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        Load.Text = Lang.Text("Download.Version.Quilt.LoadingList");
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, CardVersions, CardTip, ModDownload.DlQuiltListLoader, _ => Load_OnFinish());
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
            var Versions = (JsonArray)ModDownload.DlQuiltListLoader.Output.Value["installer"];
            PanVersions.Children.Clear();
            foreach (var Version in Versions)
                PanVersions.Children.Add(
                    ModDownloadLib.QuiltDownloadListItem((JsonObject)Version,
                        (a, b) => this.Quilt_Selected((MyListItem)a, b)));
            CardVersions.Title = Lang.Text("Download.Version.VersionListCount", Versions.Count);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Quilt 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    private void Quilt_Selected(MyListItem sender, EventArgs e)
    {
        ModDownloadLib.McDownloadQuiltLoaderSave((JsonObject)sender.Tag);
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://quiltmc.org");
    }
}