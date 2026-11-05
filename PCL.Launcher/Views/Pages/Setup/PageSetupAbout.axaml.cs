using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

public partial class PageSetupAbout : MyPageRight
{
    private bool _contributorsLoaded;
    private bool _loaded;

    public PageSetupAbout()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        ItemAboutPcl.Title = Basics.Metadata.Name;

        BtnSponsorOriginalAuthor.Click += (_, _) => Basics.OpenPath("https://ifdian.net/a/LTCat");
        BtnGitHubHome.Click += (_, _) => Basics.OpenPath("https://github.com/PCL-Community");
        BtnSourceCode.Click +=
            (_, _) => Basics.OpenPath("https://github.com/PCL-Community/PCL2-CE");
        BtnContributors.Click += (_, _) =>
            Basics.OpenPath("https://github.com/PCL-Community/PCL2-CE/graphs/contributors");
        BtnCommunitySource.Click +=
            (_, _) => Basics.OpenPath("https://github.com/PCL-Community/PCL2-CE");
        BtnCommunityLobbyPrivacy.Click += (_, _) =>
            Basics.OpenPath("https://www.pclc.cc/privacy/personal-info-brief.html");
        BtnNatayarkTerms.Click += (_, _) => Basics.OpenPath("https://account.naids.com/policy");
        BtnUpstreamTerms.Click +=
            (_, _) => Basics.OpenPath("https://shimo.im/docs/rGrd8pY8xWkt6ryW");
        BtnUpstreamSource.Click += (_, _) => Basics.OpenPath("https://github.com/Meloong-Git/PCL");
        BtnSponsorMirror.Click += (_, _) => Basics.OpenPath("https://afdian.com/a/bangbang93");
        BtnMcmod.Click += (_, _) => Basics.OpenPath("https://www.mcmod.cn");
        BtnPysioBlog.Click += (_, _) => Basics.OpenPath("https://www.pysio.online");
        BtnYunmoanWebsite.Click += (_, _) => Basics.OpenPath("https://www.zyghit.cn");
        BtnEasyTierWebsite.Click += (_, _) => Basics.OpenPath("https://easytier.cn");

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            ViewModel.Initialize();
            RefreshMcmodRow();
            _ = LoadContributorsAsync();
        };
    }

    public PageSetupAboutViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        if (!_loaded) return;
        ViewModel.Initialize();
        RefreshMcmodRow();
        if (PanContributors.Children.Count == 0)
            _ = LoadContributorsAsync();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (!_loaded) return;
        ViewModel.Initialize();
        ViewModel.RefreshVersionInfo();
        RefreshMcmodRow();
        ItemAboutPcl.Title = Basics.Metadata.Name;
    }

    private void RefreshMcmodRow()
    {
        var show = ViewModel.ShowMcmod;
        ImgMcmod.IsVisible = show;
        ItemMcmod.IsVisible = show;
        BtnMcmod.IsVisible = show;
        if (PanThanks.RowDefinitions.Count > 1)
            PanThanks.RowDefinitions[1].Height = show
                ? new GridLength(45d)
                : new GridLength(0d);
    }

    #region 彩蛋

    private void ImgPCLCommunity_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        PageSetupAboutViewModel.RotateCommunityLogo(sender);
    }

    private async void ImgPCLLogo_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        await ViewModel.HandleLogoClickAsync(sender);
    }

    #endregion

    #region 贡献者

    private async Task LoadContributorsAsync()
    {
        if (_contributorsLoaded) return;
        _contributorsLoaded = true;
        try
        {
            await ViewModel.LoadContributorsAsync();
            if (ViewModel.Error is not null)
                throw new InvalidOperationException(ViewModel.Error);
            RebuildContributors();
        }
        catch (Exception ex)
        {
            _contributorsLoaded = false;
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.About.Error.LoadContributorsFailed"),
                LauncherLogLevel.Hint);
            HintService.Show(
                Hints.Error(Lang.Text("Setup.About.Error.LoadContributorsFailed"), false));
        }
    }

    private void RebuildContributors()
    {
        PanContributors.Children.Clear();
        foreach (var contributor in ViewModel.Contributors)
            PanContributors.Children.Add(
                CreateContributorItem(
                    contributor.Login,
                    contributor.AvatarUrl,
                    contributor.HtmlUrl));
    }

    private static Control CreateContributorItem(string login, string? avatarUrl, string? htmlUrl)
    {
        var panel = new StackPanel
        {
            Width = 72d,
            Margin = new Thickness(4, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Vertical
        };
        panel.Children.Add(
            new MyImage
            {
                Width = 36d,
                Height = 36d,
                CornerRadius = new CornerRadius(18d),
                HorizontalAlignment = HorizontalAlignment.Center,
                Source = avatarUrl
            });
        var button = new MyTextButton
        {
            MaxWidth = 90d,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 14d,
            Text = login
        };
        ToolTip.SetTip(button, login);
        var target = htmlUrl;
        button.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(target))
                Basics.OpenPath(target);
        };
        panel.Children.Add(button);
        return panel;
    }

    #endregion

    #region 开源许可

    private void LicenseWebsite_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is MyButton { DataContext: ThirdPartyLicenseModel license }
            && !string.IsNullOrEmpty(license.WebsiteUri))
            Basics.OpenPath(license.WebsiteUri);
    }

    private void LicenseUri_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is MyButton { DataContext: ThirdPartyLicenseModel license }
            && !string.IsNullOrEmpty(license.LicenseUri))
            Basics.OpenPath(license.LicenseUri);
    }

    #endregion
}