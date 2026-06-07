using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;

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
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.dlOptiFineListLoader, _ => Load_OnFinish());
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
            const string snapshotKey = "Snapshot";
            // 归类
            var dict = new Dictionary<string, List<ModDownload.DlOptiFineListEntry>>();
            dict.Add(snapshotKey, new List<ModDownload.DlOptiFineListEntry>());
            for (var versionCode = 50; versionCode >= 0; versionCode -= 1)
                dict.Add("1." + versionCode, new List<ModDownload.DlOptiFineListEntry>());
            foreach (var Version in ModDownload.dlOptiFineListLoader.output.Value)
                if (Version.Inherit.StartsWith("1."))
                {
                    var mainVersion = "1." + Version.DisplayName.Split(".")[1].Split(" ")[0];
                    if (dict.ContainsKey(mainVersion))
                        dict[mainVersion].Add(Version);
                    else
                        dict[snapshotKey].Add(Version);
                }
                else
                {
                    dict[snapshotKey].Add(Version);
                }

            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var Pair in dict)
            {
                if (!Pair.Value.Any())
                    continue;
                // 增加卡片
                var title = Pair.Key == snapshotKey
                    ? Lang.Text("Download.Version.Optifine.Snapshot")
                    : Pair.Key;
                var newCard = new MyCard
                    { Title = title + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = Pair.Value
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                newCard.IsSwapped = true;
                newCard.InstallMethod = stack =>
                {
                    stack.Tag = ((List<ModDownload.DlOptiFineListEntry>)stack.Tag).Sort((a, b) =>
                        McVersionComparer.CompareVersion(a.DisplayName, b.DisplayName) == 1);
                    foreach (var item in (IEnumerable)stack.Tag)
                        stack.Children.Add(ModDownloadLib.OptiFineDownloadListItem(
                            (ModDownload.DlOptiFineListEntry)item, ModDownloadLib.OptiFineSave_Click, true));
                };
                PanMain.Children.Add(newCard);
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