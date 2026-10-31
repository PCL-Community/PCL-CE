using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面「关于」页的视图模型。</summary>
public partial class PageSetupAboutViewModel : ViewModelBase
{
    private int _clickCount;

    private bool _initialized;

    [ObservableProperty] private string _aboutInfo = FormatVersionInfo();

    [ObservableProperty] private bool _showMcmod = true;

    [ObservableProperty] private bool _isLogoClickEnabled = true;

    public ObservableCollection<PageSetupAbout.GitHubContributor> Contributors { get; } = [];

    public void Initialize()
    {
        ShowMcmod = Lang.IsChineseMainland;
        if (_initialized) return;
        _initialized = true;
        LoadContributorsCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadContributorsAsync()
    {
        var contributors = await RunAsync(ct => Task.Run(FetchContributorsAsync, ct));
        if (contributors is null) return;
        Contributors.Clear();
        foreach (var contributor in contributors)
            Contributors.Add(contributor);
    }

    [RelayCommand]
    public void RotateCommunityLogo(object? sender)
    {
        if (sender is null) return;
        Animations.AniStart(new[] { Animations.AaRotateTransform(sender, 360d) });
    }

    [RelayCommand]
    public async Task LogoClickAsync(object? sender)
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
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.NiceClick"));
                        break;
                    case 15:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.StillClicking"));
                        break;
                    case 25:
                        var boredResult = await Dialog.ShowAsync(new TextDialogRequest
                        {
                            Title = Lang.Text("Setup.About.EasterEgg.Bored.Title"),
                            Content = Lang.Text("Setup.About.EasterEgg.Bored.Message"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Bored.Yes")),
                                DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Bored.No")))
                        });
                        if (boredResult.Button == 2)
                            HintService.Hint(Lang.Text("Setup.About.EasterEgg.Bored.Response"));
                        break;
                    case 50:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.Encouragement"));
                        break;
                    case 75:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.HiddenTheme"));
                        break;
                    case 100:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.StillStaring"));
                        break;
                    case 130:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.NothingBehind"));
                        break;
                    case 150:
                        var tiredResult1 = await Dialog.ShowAsync(new TextDialogRequest
                        {
                            Title = Lang.Text("Setup.About.EasterEgg.Tired.Title1"),
                            Content = Lang.Text("Setup.About.EasterEgg.Tired.Message1"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Tired.Exhausted")),
                                DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                        });
                        switch (tiredResult1.Button)
                        {
                            case 1:
                                HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                break;
                            case 2:
                                var tiredResult2 = await Dialog.ShowAsync(new TextDialogRequest
                                {
                                    Title = Lang.Text("Setup.About.EasterEgg.Tired.Title2"),
                                    Content = Lang.Text("Setup.About.EasterEgg.Tired.Message2"),
                                    Buttons = DialogButtons.From(
                                        DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Tired.Exhausted")),
                                        DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                                });
                                switch (tiredResult2.Button)
                                {
                                    case 1:
                                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                        break;
                                    case 2:
                                        var tiredResult3 = await Dialog.ShowAsync(new TextDialogRequest
                                        {
                                            Title = Lang.Text("Setup.About.EasterEgg.Tired.Title3"),
                                            Content = Lang.Text("Setup.About.EasterEgg.Tired.Message3"),
                                            Buttons = DialogButtons.From(
                                                DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Tired.Exhausted")),
                                                DialogButton.Create(Lang.Text("Setup.About.EasterEgg.Tired.ReallyNotTired")))
                                        });
                                        switch (tiredResult3.Button)
                                        {
                                            case 1:
                                                HintService.Hint(
                                                    Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                                break;
                                            case 2:
                                                HintService.Hint(
                                                    Lang.Text("Setup.About.EasterEgg.Tired.FinallyGiveUp"));
                                                break;
                                        }

                                        break;
                                }

                                break;
                        }

                        break;
                    case 200:
                        HintService.Hint(Lang.Text("Setup.About.EasterEgg.ClickDisabled"));
                        IsLogoClickEnabled = false;
                        return;
                }

                var rand = new Random();
                var mx = rand.Next(-1, 1);
                if (mx == 0)
                    mx = 1;
                var my = rand.Next(-1, 1);
                if (my == 0)
                    my = 1;
                Animations.AniStart(new[]
                {
                    Animations.AaTranslateX(sender, mx, 0), Animations.AaTranslateY(sender, my, 0),
                    Animations.AaTranslateX(sender, -mx, 0, 100), Animations.AaTranslateY(sender, -my, 0, 100)
                });
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "处理彩蛋点击时失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private static string FormatVersionInfo()
    {
        return Lang.Text("Setup.About.Version.Info").Replace("%VERSION%", Common.VersionBaseName)
            .Replace("%VERSIONCODE%", Common.VersionCode.ToString())
            .Replace("%BRANCH%", Common.VersionBranchName)
            .Replace("%COMMIT_HASH%", Common.CommitHashShort);
    }

    private static async Task<List<PageSetupAbout.GitHubContributor>> FetchContributorsAsync()
    {
        using var response = await HttpRequest
            .Create("https://api.github.com/repos/PCL-Community/PCL2-CE/contributors").SendAsync();
        response.EnsureSuccessStatusCode();
        return await response.AsJsonAsync<List<PageSetupAbout.GitHubContributor>>(JsonCompat.SerializerOptions) ?? [];
    }
}
