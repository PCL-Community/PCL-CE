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
        Load.Text = Lang.Text("Download.Version.Forge.LoadingList");
        BtnWeb.Click += BtnWeb_Click;
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanMain, CardTip, ModDownload.DlForgeListLoader, _ => Load_OnFinish());
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
            foreach (var Version in ModDownload.DlForgeListLoader.Output.Value.Sort(ModMinecraft.CompareVersionGe))
            {
                // 增加卡片
                var NewCard = new MyCard
                    { Title = Version.Replace("_p", " P"), Margin = new Thickness(0d, 0d, 0d, 15d) };
                var NewStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = Version
                };
                NewCard.Children.Add(NewStack);
                NewCard.SwapControl = NewStack;
                NewCard.InstallMethod = Stack =>
                {
                    var LoadingPickaxe = new MyLoading { Text = Lang.Text("Download.Version.Forge.LoadingList"), Margin = new Thickness(5d) };
                    var Loader =
                        new ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>("DlForgeVersion Main",
                            ModDownload.DlForgeVersionMain);
                    LoadingPickaxe.State = Loader;
                    Loader.Start(Stack.Tag);
                    LoadingPickaxe.StateChanged += (a, b, c) =>
                        ModMain.FrmDownloadForge.Forge_StateChanged((MyLoading)a, b, c);
                    LoadingPickaxe.Click += (a, b) => ModMain.FrmDownloadForge.Forge_Click((MyLoading)a, b);
                    Stack.Children.Add(LoadingPickaxe);
                };
                NewCard.IsSwapped = true;
                PanMain.Children.Add(NewCard);
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

        var Card = (MyCard)((FrameworkElement)sender.Parent).Parent;
        var Loader = (ModLoader.LoaderTask<string, List<ModDownload.DlForgeVersionEntry>>)sender.State;
        // 载入列表
        ((StackPanel)Card.SwapControl).Children.Clear();
        ((StackPanel)Card.SwapControl).Tag = Loader.Output;
        Card.InstallMethod = Stack =>
        {
            Stack.Tag = ((List<ModDownload.DlForgeVersionEntry>)Stack.Tag).Sort((a, b) => a.Version > b.Version);
            ModDownloadLib.ForgeDownloadListItemPreload(Stack, (List<ModDownload.DlForgeVersionEntry>)Stack.Tag,
                ModDownloadLib.ForgeSave_Click, true);
            foreach (var item in (IEnumerable)Stack.Tag)
                Stack.Children.Add(ModDownloadLib.ForgeDownloadListItem((ModDownload.DlForgeVersionEntry)item,
                    ModDownloadLib.ForgeSave_Click, true));
        };
        Card.StackInstall();
    }

    // 介绍栏
    private void BtnWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://files.minecraftforge.net");
    }
}