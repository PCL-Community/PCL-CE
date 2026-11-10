using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Network;

namespace PCL;

/// <summary>设置页面「关于」页的视图模型。</summary>
public partial class PageSetupAboutViewModel : ViewModelBase
{
    private const string ContributorsApi =
        "https://api.github.com/repos/PCL-Community/PCL2-CE/contributors";

    [ObservableProperty] private string _aboutInfo = FormatVersionInfo();

    private int _clickCount;

    [ObservableProperty] private bool _isLogoClickEnabled = true;

    [ObservableProperty] private bool _showMcmod = true;

    public ObservableCollection<GitHubContributor> Contributors { get; } = [];

    public IReadOnlyList<ThirdPartyLicenseModel> Licenses => Basics.Metadata.Licenses;

    public void RefreshVersionInfo()
    {
        AboutInfo = FormatVersionInfo();
    }

    public void Initialize()
    {
        ShowMcmod = Lang.IsChineseMainland;
    }

    public async Task LoadContributorsAsync()
    {
        List<GitHubContributor> contributors;
        try
        {
            contributors = await Task.Run(
                () => FetchContributorsAsync(CancellationToken.None));
        }
        catch (Exception ex)
        {
            var message = Lang.Text("Setup.About.Error.LoadContributorsFailed");
            LogWrapper.Warn(ex, message);
            HintService.Show(Hints.Error(message));
            return;
        }

        Contributors.Clear();
        foreach (var contributor in contributors)
            Contributors.Add(contributor);
    }

    public static void RotateCommunityLogo(object? sender)
    {
        if (sender is null) return;
        Animations.AniStart(
            new[]
            {
                Animations.AaRotateTransform(sender, 360d)
            });
    }

    public async Task HandleLogoClickAsync(object? sender)
    {
        if (!IsLogoClickEnabled || sender is null) return;
        try
        {
            if (_clickCount < 200)
            {
                _clickCount += 1;
                switch (_clickCount)
                {
                    case 5:
                        HintService.Show(Hints.Info(Lang.Text("Setup.About.EasterEgg.NiceClick")));
                        break;
                    case 15:
                        HintService.Show(
                            Hints.Info(Lang.Text("Setup.About.EasterEgg.StillClicking")));
                        break;
                    case 25:
                        var boredResult = await Dialog.ShowAsync(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Setup.About.EasterEgg.Bored.Title"),
                                Content = Lang.Text("Setup.About.EasterEgg.Bored.Message"),
                                Buttons = DialogButtons.From(
                                    DialogButton.Create(
                                        Lang.Text("Setup.About.EasterEgg.Bored.Yes")),
                                    DialogButton.Create(
                                        Lang.Text("Setup.About.EasterEgg.Bored.No")))
                            });
                        if (boredResult.Button == 2)
                            HintService.Show(
                                Hints.Info(Lang.Text("Setup.About.EasterEgg.Bored.Response")));
                        break;
                    case 50:
                        HintService.Show(
                            Hints.Info(Lang.Text("Setup.About.EasterEgg.Encouragement")));
                        break;
                    case 75:
                        HintService.Show(
                            Hints.Info(Lang.Text("Setup.About.EasterEgg.HiddenTheme")));
                        break;
                    case 100:
                        HintService.Show(
                            Hints.Info(Lang.Text("Setup.About.EasterEgg.StillStaring")));
                        break;
                    case 130:
                        HintService.Show(
                            Hints.Info(Lang.Text("Setup.About.EasterEgg.NothingBehind")));
                        break;
                    case 150:
                        var tiredResult1 = await Dialog.ShowAsync(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Setup.About.EasterEgg.Tired.Title1"),
                                Content = Lang.Text("Setup.About.EasterEgg.Tired.Message1"),
                                Buttons = DialogButtons.From(
                                    DialogButton.Create(
                                        Lang.Text("Setup.About.EasterEgg.Tired.Exhausted")),
                                    DialogButton.Create(
                                        Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                            });
                        switch (tiredResult1.Button)
                        {
                            case 1:
                                HintService.Show(
                                    Hints.Info(
                                        Lang.Text("Setup.About.EasterEgg.Tired.StopClicking")));
                                break;
                            case 2:
                                var tiredResult2 = await Dialog.ShowAsync(
                                    new TextDialogRequest
                                    {
                                        Title = Lang.Text("Setup.About.EasterEgg.Tired.Title2"),
                                        Content = Lang.Text("Setup.About.EasterEgg.Tired.Message2"),
                                        Buttons = DialogButtons.From(
                                            DialogButton.Create(
                                                Lang.Text("Setup.About.EasterEgg.Tired.Exhausted")),
                                            DialogButton.Create(
                                                Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                                    });
                                switch (tiredResult2.Button)
                                {
                                    case 1:
                                        HintService.Show(
                                            Hints.Info(
                                                Lang.Text(
                                                    "Setup.About.EasterEgg.Tired.StopClicking")));
                                        break;
                                    case 2:
                                        var tiredResult3 = await Dialog.ShowAsync(
                                            new TextDialogRequest
                                            {
                                                Title = Lang.Text(
                                                    "Setup.About.EasterEgg.Tired.Title3"),
                                                Content = Lang.Text(
                                                    "Setup.About.EasterEgg.Tired.Message3"),
                                                Buttons = DialogButtons.From(
                                                    DialogButton.Create(
                                                        Lang.Text(
                                                            "Setup.About.EasterEgg.Tired.Exhausted")),
                                                    DialogButton.Create(
                                                        Lang.Text(
                                                            "Setup.About.EasterEgg.Tired.ReallyNotTired")))
                                            });
                                        switch (tiredResult3.Button)
                                        {
                                            case 1:
                                                HintService.Show(
                                                    Hints.Info(
                                                        Lang.Text(
                                                            "Setup.About.EasterEgg.Tired.StopClicking")));
                                                break;
                                            case 2:
                                                HintService.Show(
                                                    Hints.Info(
                                                        Lang.Text(
                                                            "Setup.About.EasterEgg.Tired.FinallyGiveUp")));
                                                break;
                                        }

                                        break;
                                }

                                break;
                        }

                        break;
                    case 200:
                        HintService.Show(
                            Hints.Info(Lang.Text("Setup.About.EasterEgg.ClickDisabled")));
                        IsLogoClickEnabled = false;
                        return;
                }

                var rand = new Random();
                var mx = rand.Next(-1, 1);
                if (mx == 0) mx = 1;
                var my = rand.Next(-1, 1);
                if (my == 0) my = 1;
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaTranslateX(sender, mx, 0),
                        Animations.AaTranslateY(sender, my, 0),
                        Animations.AaTranslateX(sender, -mx, 0, 100),
                        Animations.AaTranslateY(sender, -my, 0, 100)
                    });
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "处理彩蛋点击时失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
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

    private static async Task<List<GitHubContributor>> FetchContributorsAsync(
        CancellationToken cancellationToken)
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

        var result = new List<GitHubContributor>();
        foreach (var node in list)
        {
            if (node is not JsonObject contributor) continue;
            var login = contributor["login"]?.ToString();
            if (string.IsNullOrEmpty(login)) continue;
            result.Add(
                new GitHubContributor
                {
                    Login = login,
                    AvatarUrl = contributor["avatar_url"]?.ToString() ?? string.Empty,
                    HtmlUrl = contributor["html_url"]?.ToString() ?? string.Empty,
                    Contributions = int.TryParse(
                        contributor["contributions"]?.ToString(),
                        out var contributions)
                        ? contributions
                        : 0
                });
        }

        return result;
    }

    public sealed class GitHubContributor
    {
        [JsonPropertyName("login")] public string Login { get; init; } = string.Empty;

        [JsonPropertyName("avatar_url")] public string AvatarUrl { get; init; } = string.Empty;

        [JsonPropertyName("html_url")] public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("contributions")] public int Contributions { get; init; }
    }
}