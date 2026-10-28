using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>界面个性化设置页的视图模型。</summary>
public partial class PageSetupUIViewModel : ViewModelBase
{
    [ObservableProperty] private Visibility _launcherHideVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _backgroundOpacityVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _backgroundBlurVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _backgroundSuitVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _backgroundClearVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _autoPauseVideoVisibility = Visibility.Visible;

    [ObservableProperty] private string _backgroundTitle = Lang.Text("Setup.Ui.Background.TitleDefault");

    [ObservableProperty] private Visibility _musicVolumeVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _musicDetailVisibility = Visibility.Collapsed;

    [ObservableProperty] private Visibility _musicClearVisibility = Visibility.Collapsed;

    [ObservableProperty] private string _musicTitle = Lang.Text("Setup.Ui.Music.Title");

    [ObservableProperty] private Visibility _featureHideVisibility = Visibility.Visible;

    [ObservableProperty] private string _featureHideTitle = Lang.Text("Setup.Ui.FeatureHide.Title");

    public string[] ThemeColors => Basics.IsAprilFool
        ? [Lang.Text("Setup.Ui.Theme.Color.SkyBlue"), Lang.Text("Setup.Ui.Theme.Color.CatBlue"), Lang.Text("Setup.Ui.Theme.Color.CrashBlue"), Lang.Text("Setup.Ui.Theme.Color.Hmcl")]
        : [Lang.Text("Setup.Ui.Theme.Color.SkyBlue"), Lang.Text("Setup.Ui.Theme.Color.CatBlue"), Lang.Text("Setup.Ui.Theme.Color.CrashBlue")];

    public void ShowLauncherHint()
    {
        LauncherHideVisibility = Visibility.Visible;
    }

    public void ApplyBackgroundState(bool show, int count)
    {
        if (show)
        {
            BackgroundOpacityVisibility = Visibility.Visible;
            BackgroundBlurVisibility = Visibility.Visible;
            BackgroundSuitVisibility = Visibility.Visible;
            BackgroundClearVisibility = Visibility.Visible;
            AutoPauseVideoVisibility = Visibility.Visible;
            BackgroundTitle = Lang.Text("Setup.Ui.Background.TitleWithCount", count);
        }
        else
        {
            BackgroundOpacityVisibility = Visibility.Collapsed;
            BackgroundBlurVisibility = Visibility.Collapsed;
            BackgroundSuitVisibility = Visibility.Collapsed;
            BackgroundClearVisibility = Visibility.Collapsed;
            AutoPauseVideoVisibility = Visibility.Collapsed;
            BackgroundTitle = Lang.Text("Setup.Ui.Background.TitleDefault");
        }
    }

    public void ApplyMusicState(bool hasMusic, int count)
    {
        if (hasMusic)
        {
            MusicVolumeVisibility = Visibility.Visible;
            MusicDetailVisibility = Visibility.Visible;
            MusicClearVisibility = Visibility.Visible;
            MusicTitle = Lang.Text("Setup.Ui.Music.TitleWithCount", count);
        }
        else
        {
            MusicVolumeVisibility = Visibility.Collapsed;
            MusicDetailVisibility = Visibility.Collapsed;
            MusicClearVisibility = Visibility.Collapsed;
            MusicTitle = Lang.Text("Setup.Ui.Music.Title");
        }
    }

    public void ApplyFeatureHideState(bool forceShow, bool functionHidden)
    {
        FeatureHideVisibility = !forceShow && functionHidden ? Visibility.Collapsed : Visibility.Visible;
        FeatureHideTitle = forceShow
            ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled")
            : Lang.Text("Setup.Ui.FeatureHide.Title");
    }

    public async Task<bool> ConfirmBackgroundClearAsync()
    {
        return await ModMain.MyMsgBoxAsync(Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
            Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1;
    }

    public async Task<bool> ConfirmMusicClearAsync()
    {
        return await ModMain.MyMsgBoxAsync(Lang.Text("Setup.Ui.Music.Clear.Confirm.Message"),
            Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1;
    }

    public static object FormatMusicVolumeHint(object value)
    {
        return Lang.Number(Math.Ceiling(Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    public static object FormatLauncherOpacityHint(object value)
    {
        return Lang.Number(Math.Round(40 + Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    public static object FormatBackgroundOpacityHint(object value)
    {
        return Lang.Number(Math.Round(Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    public static object FormatPixelHint(object value)
    {
        return Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(value), "N0"));
    }

    public static object FormatSamplingRateHint(object value)
    {
        return Lang.Number(Convert.ToDouble(value) / 100d, "P0");
    }
}
