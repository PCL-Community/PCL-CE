using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using PCL.Core.App.Localization;
namespace PCL;

public partial class PageDownloadNeoForge
{
    public PageDownloadNeoForge()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.dlNeoForgeListLoader, _ => Load_OnFinish());
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
            var dict = ModDownload.dlNeoForgeListLoader.output.Value.GroupBy(d => d.Inherit)
                .OrderByDescending(g => g.Key).ToDictionary(g => g.Key, g => g.ToList());
            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var Pair in dict)
            {
                if (!Pair.Value.Any())
                    continue;
                // 增加卡片
                var newCard = new MyCard
                    { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
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
                    foreach (var item in (IEnumerable)stack.Tag)
                        stack.Children.Add(ModDownloadLib.NeoForgeDownloadListItem(
                            (ModDownload.DlNeoForgeListEntry)item, ModDownloadLib.NeoForgeSave_Click, true));
                };
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "可视化 NeoForge 版本列表出错",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
    }
}