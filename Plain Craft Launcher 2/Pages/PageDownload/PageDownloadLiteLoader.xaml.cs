using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;

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
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.dlLiteLoaderListLoader, _ => Load_OnFinish());
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
            var dict = new Dictionary<string, List<ModDownload.DlLiteLoaderListEntry>>();
            for (var versionCode = 30; versionCode >= 0; versionCode -= 1)
                dict.Add("1." + versionCode, new List<ModDownload.DlLiteLoaderListEntry>());
            dict.Add(ModMinecraft.UNKNOWN_VERSION_KEY, []);
            foreach (var Version in ModDownload.dlLiteLoaderListLoader.output.value)
            {
                var mainVersion = "1." + Version.inherit.Split(".")[1];
                if (dict.ContainsKey(mainVersion))
                    dict[mainVersion].Add(Version);
                else
                    dict[ModMinecraft.UNKNOWN_VERSION_KEY].Add(Version);
            }

            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var Pair in dict)
            {
                if (!Pair.Value.Any())
                    continue;
                // 增加卡片
                var newCard = new MyCard
                {
                    Title = (Pair.Key == ModMinecraft.UNKNOWN_VERSION_KEY
                        ? Lang.Text("Minecraft.Version.Unknown")
                        : Pair.Key) + " (" + Pair.Value.Count + ")",
                    Margin = new Thickness(0d, 0d, 0d, 15d)
                };
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
                    stack.Tag = ((List<ModDownload.DlLiteLoaderListEntry>)stack.Tag).Sort((a, b) =>
                        ModMinecraft.CompareVersion(a.inherit, b.inherit) == 1);
                    foreach (var item in (IEnumerable)stack.Tag)
                        stack.Children.Add(ModDownloadLib.LiteLoaderDownloadListItem(
                            (ModDownload.DlLiteLoaderListEntry)item, ModDownloadLib.LiteLoaderSave_Click, true));
                };
                PanMain.Children.Add(newCard);
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