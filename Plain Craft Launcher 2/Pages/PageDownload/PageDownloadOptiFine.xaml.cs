using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public partial class PageDownloadOptiFine
{
    public PageDownloadOptiFine()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.DlOptiFineListLoader, _ => Load_OnFinish());
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
            var Dict = new Dictionary<string, List<ModDownload.DlOptiFineListEntry>>();
            Dict.Add("快照版本", new List<ModDownload.DlOptiFineListEntry>());
            for (var VersionCode = 50; VersionCode >= 0; VersionCode -= 1)
                Dict.Add("1." + VersionCode, new List<ModDownload.DlOptiFineListEntry>());
            foreach (var Version in ModDownload.DlOptiFineListLoader.Output.Value)
                if (Version.Inherit.StartsWith("1."))
                {
                    var MainVersion = "1." + Version.DisplayName.Split(".")[1].Split(" ")[0];
                    if (Dict.ContainsKey(MainVersion))
                        Dict[MainVersion].Add(Version);
                    else
                        Dict["快照版本"].Add(Version);
                }
                else
                {
                    Dict["快照版本"].Add(Version);
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
                    Stack.Tag = ((List<ModDownload.DlOptiFineListEntry>)Stack.Tag).Sort((a, b) =>
                        ModMinecraft.CompareVersion(a.DisplayName, b.DisplayName) == 1);
                    foreach (var item in (IEnumerable)Stack.Tag)
                        Stack.Children.Add(ModDownloadLib.OptiFineDownloadListItem(
                            (ModDownload.DlOptiFineListEntry)item, ModDownloadLib.OptiFineSave_Click, true));
                };
                PanMain.Children.Add(NewCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 OptiFine 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://www.optifine.net/");
    }
}