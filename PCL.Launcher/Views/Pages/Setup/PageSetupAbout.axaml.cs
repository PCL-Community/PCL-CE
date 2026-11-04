using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

public partial class PageSetupAbout : MyPageRight
{
    private const string ContributorsApi =
        "https://api.github.com/repos/PCL-Community/PCL2-CE/contributors";

    private bool _contributorsLoaded;
    private bool _loaded;

    public PageSetupAbout()
    {
        InitializeComponent();
        PanScroll = PanBack;

        ItemAboutPcl.Title = Basics.Metadata.Name;
        ItemAboutPcl.Info = FormatVersionInfo();

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
            BuildLicenses();
            RefreshMcmodRow();
            _ = LoadContributorsAsync();
        };
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (!_loaded) return;
        ItemAboutPcl.Info = FormatVersionInfo();
        BuildLicenses();
    }

    private static string FormatVersionInfo()
    {
        return Lang
            .Text("Setup.About.Version.Info")
            .Replace("%VERSION%", LauncherCommon.VersionBaseName)
            .Replace("%VERSIONCODE%", LauncherCommon.VersionCode.ToString())
            .Replace("%BRANCH%", LauncherCommon.VersionBranchName)
            .Replace("%COMMIT_HASH%", Basics.Metadata.Version.CommitDigest);
    }

    private void RefreshMcmodRow()
    {
        var show = Lang.IsChineseMainland;
        ImgMcmod.IsVisible = show;
        ItemMcmod.IsVisible = show;
        BtnMcmod.IsVisible = show;
        if (PanThanks.RowDefinitions.Count > 1)
            PanThanks.RowDefinitions[1].Height = show
                ? new GridLength(45d)
                : new GridLength(0d);
    }

    #region 贡献者

    private async Task LoadContributorsAsync()
    {
        if (_contributorsLoaded) return;
        _contributorsLoaded = true;
        try
        {
            var json = await Requester.FetchJsonAsync(
                ContributorsApi,
                new RequestParam
                {
                    Retries = 2,
                    UseBrowserUserAgent = true
                });
            if (json is not JsonArray list)
                throw new InvalidOperationException(
                    Lang.Text("Setup.About.Error.LoadContributorsFailed"));

            PanContributors.Children.Clear();
            foreach (var node in list)
            {
                if (node is not JsonObject contributor) continue;
                var login = contributor["login"]?.ToString();
                var avatar = contributor["avatar_url"]?.ToString();
                var htmlUrl = contributor["html_url"]?.ToString();
                if (string.IsNullOrEmpty(login)) continue;
                PanContributors.Children.Add(CreateContributorItem(login, avatar, htmlUrl));
            }
        }
        catch (Exception ex)
        {
            // TODO(FIX-P3-07): 失败后没有重试入口；WPF 由 ViewModel 命令重新拉取，可接一个"重试"按钮或下次进入重试。
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.About.Error.LoadContributorsFailed"),
                LauncherLogLevel.Hint);
            HintService.Show(
                Hints.Error(Lang.Text("Setup.About.Error.LoadContributorsFailed"), false));
        }
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

    private void BuildLicenses()
    {
        PanLicenses.Children.Clear();
        foreach (var license in Basics.Metadata.Licenses)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,20,Auto,*"),
                RowDefinitions = new RowDefinitions("Auto,60"),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var name = new TextBlock
            {
                Text = license.Name,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(name, 0);
            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            var info = new TextBlock
            {
                Text = license.Information,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(info, 0);
            Grid.SetColumn(info, 2);
            Grid.SetColumnSpan(info, 2);
            grid.Children.Add(info);

            if (!string.IsNullOrEmpty(license.WebsiteUri))
            {
                var button = CreateLinkButton(
                    Lang.Text("Setup.About.Licenses.ViewWebsite"),
                    license.WebsiteUri);
                Grid.SetRow(button, 1);
                Grid.SetColumn(button, 2);
                grid.Children.Add(button);
            }

            if (!string.IsNullOrEmpty(license.LicenseUri))
            {
                var button = CreateLinkButton(
                    Lang.Text("Setup.About.Licenses.ViewLicense"),
                    license.LicenseUri);
                Grid.SetRow(button, 1);
                Grid.SetColumn(button, string.IsNullOrEmpty(license.WebsiteUri) ? 2 : 3);
                grid.Children.Add(button);
            }

            PanLicenses.Children.Add(grid);
        }
    }

    private static MyButton CreateLinkButton(string text, string url)
    {
        var button = new MyButton
        {
            Text = text,
            MinWidth = 140d,
            Margin = new Thickness(0, 7, 20, 18),
            Padding = new Thickness(13, 0, 13, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) => Basics.OpenPath(url);
        return button;
    }

    #endregion
}