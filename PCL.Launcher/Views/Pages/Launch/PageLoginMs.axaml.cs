using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     正版（微软）登录视图：登录状态与设备码登录由 <see cref="PageLoginMsViewModel" /> 负责，
///     页面只转发导航事件、打开购买/官网链接与处理本地化刷新。
/// </summary>
public partial class PageLoginMs : StackPanel, ILoginPage, ILoginPage_Bindable
{
    public PageLoginMs()
    {
        InitializeComponent();
        DataContext = ViewModel;

        ViewModel.BackRequested += RequestBack;
        ViewModel.RefreshRequested += RequestRefresh;

        this.FindControl<MyTextButton>("BtnPurchase")!.Click += (_, _) =>
            LauncherUi.Interaction?.OpenWebsite(
                "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
        this.FindControl<MyTextButton>("BtnWebsite")!.Click += (_, _) =>
            LauncherUi.Interaction?.OpenWebsite("https://www.minecraft.net/zh-hans");
        ResetLoginState();

        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += OnLanguageChanged;
        DetachedFromVisualTree +=
            (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    public PageLoginMsViewModel ViewModel { get; } = new();

    public void Reload()
    {
        ViewModel.ResetLoginState();
    }

    public event Action? BackRequested;

    public event Action<GameLaunch.McLoginType>? LoginTypeRequested;

    public event Action? RefreshRequested;

    private void OnLanguageChanged()
    {
        if (!ViewModel.IsLoggingIn)
            ViewModel.ResetLoginState();
    }

    private void RequestBack()
    {
        BackRequested?.Invoke();
    }

    private void RequestRefresh()
    {
        RefreshRequested?.Invoke();
    }

    private void ResetLoginState()
    {
        ViewModel.ResetLoginState();
    }
}