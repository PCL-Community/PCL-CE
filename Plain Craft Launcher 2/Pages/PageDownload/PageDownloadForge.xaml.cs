using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadForge
{
    public PageDownloadForge()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.dlForgeListLoader, _ => Load_OnFinish());
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
            // 清空当前
            PanMain.Children.Clear();
            // 转化为 UI
            foreach (var Version in ModDownload.dlForgeListLoader.output.value.Sort(ModMinecraft.CompareVersionGe))
            {
                // 增加卡片
                var newCard = new MyCard
                    { Title = Version.Replace("_p", " P"), Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = Version
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                newCard.InstallMethod = Stack =>
                {
                    var loadingPickaxe = new MyLoading { Text = Lang.Text("Download.Version.Forge.LoadingList"), Margin = new Thickness(5d) };
                    var loader =
                        new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>("DlForgeVersion Main",
                            ModDownload.DlForgeVersionMain);
                    loadingPickaxe.State = loader;
                    loader.Start(Stack.Tag);
                    loadingPickaxe.StateChanged += (a, b, c) =>
                        ModMain.frmDownloadForge.Forge_StateChanged((MyLoading)a, b, c);
                    loadingPickaxe.Click += (a, b) => ModMain.frmDownloadForge.Forge_Click((MyLoading)a, b);
                    Stack.Children.Add(loadingPickaxe);
                };
                newCard.IsSwapped = true;
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 Forge 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }

    // Forge 版本列表加载
    public void Forge_Click(MyLoading sender, MouseButtonEventArgs e)
    {
        if (sender.State.LoadingState == MyLoading.MyLoadingState.Error)
            ((ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)sender.State).Start(
                IsForceRestart: true);
    }

    public void Forge_StateChanged(MyLoading sender, MyLoading.MyLoadingState newState,
        MyLoading.MyLoadingState oldState)
    {
        if (newState != MyLoading.MyLoadingState.Stop)
            return;

        var card = (MyCard)((FrameworkElement)sender.Parent).Parent;
        var loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)sender.State;
        // 载入列表
        ((StackPanel)card.SwapControl).Children.Clear();
        ((StackPanel)card.SwapControl).Tag = loader.output;
        card.InstallMethod = Stack =>
        {
            Stack.Tag = ((List<ModDownload.DlForgeVersionEntry>)Stack.Tag).Sort((a, b) => a.version > b.version);
            ModDownloadLib.ForgeDownloadListItemPreload(Stack, (List<ModDownload.DlForgeVersionEntry>)Stack.Tag,
                ModDownloadLib.ForgeSave_Click, true);
            foreach (var item in (IEnumerable)Stack.Tag)
                Stack.Children.Add(ModDownloadLib.ForgeDownloadListItem((ModDownload.DlForgeVersionEntry)item,
                    ModDownloadLib.ForgeSave_Click, true));
        };
        card.StackInstall();
    }

    // 介绍栏
    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://files.minecraftforge.net");
    }
}