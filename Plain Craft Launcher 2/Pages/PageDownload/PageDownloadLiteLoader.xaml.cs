using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public partial class PageDownloadLiteLoader
{
    public PageDownloadLiteLoader()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.DlLiteLoaderListLoader, _ => Load_OnFinish());
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
            // 归类
            var Dict = new Dictionary<string, List<ModDownload.DlLiteLoaderListEntry>>();
            for (var VersionCode = 30; VersionCode >= 0; VersionCode -= 1)
                Dict.Add("1." + VersionCode, new List<ModDownload.DlLiteLoaderListEntry>());
            Dict.Add("未知版本", new List<ModDownload.DlLiteLoaderListEntry>());
            foreach (var Version in ModDownload.DlLiteLoaderListLoader.Output.Value)
            {
                var MainVersion = "1." + Version.Inherit.Split(".")[1];
                if (Dict.ContainsKey(MainVersion))
                    Dict[MainVersion].Add(Version);
                else
                    Dict["未知版本"].Add(Version);
            }

            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var Pair in Dict)
            {
                if (!Pair.Value.Any())
                    continue;
                // 增加卡片
                var NewCard = new MyCard
                    { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                var NewStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = Pair.Value
                };
                NewCard.Children.Add(NewStack);
                NewCard.SwapControl = NewStack;
                NewCard.IsSwapped = true;
                NewCard.InstallMethod = Stack =>
                {
                    Stack.Tag = ((List<ModDownload.DlLiteLoaderListEntry>)Stack.Tag).Sort((a, b) =>
                        ModMinecraft.CompareVersion(a.Inherit, b.Inherit) == 1);
                    foreach (var item in (IEnumerable)Stack.Tag)
                        Stack.Children.Add(ModDownloadLib.LiteLoaderDownloadListItem(
                            (ModDownload.DlLiteLoaderListEntry)item, ModDownloadLib.LiteLoaderSave_Click, true));
                };
                PanMain.Children.Add(NewCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 LiteLoader 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    public void DownloadStart(MyListItem sender, object e)
    {
        ModDownloadLib.McDownloadLiteLoader((ModDownload.DlLiteLoaderListEntry)sender.Tag);
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://www.liteloader.com");
    }
}