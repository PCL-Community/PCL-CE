using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>工具测试页的视图模型。</summary>
public partial class PageToolsTestViewModel : ViewModelBase
{
    [ObservableProperty] private bool _achievementButtonsEnabled = true;

    [ObservableProperty] private bool _achievementImageVisible;

    [ObservableProperty] private bool _crashButtonVisible;

    [ObservableProperty] private bool _createShortcutEnabled = true;

    [ObservableProperty] private bool _downloadOpenEnabled = true;

    [ObservableProperty] private bool _downloadStartEnabled = true;

    [ObservableProperty] private bool _launchCountEnabled = true;

    [ObservableProperty] private bool _skinPreviewVisible;

    public PageToolsTestViewModel()
    {
#if DEBUG
        CrashButtonVisible = true;
#endif
    }

    public void RefreshAchievementButtons(bool blockValid, bool titleValid, bool string1Valid)
    {
        AchievementButtonsEnabled = blockValid && titleValid && string1Valid;
    }

    public void RefreshDownloadButtons(bool folderValid, bool urlValid, bool nameValid)
    {
        DownloadStartEnabled = folderValid && urlValid && nameValid;
        DownloadOpenEnabled = folderValid;
    }

    [RelayCommand]
    private void Crash()
    {
        throw new Exception(Lang.Text("Tools.Test.Crash.ManualCrash"));
    }

    [RelayCommand]
    private async Task CreateShortcutAsync()
    {
        CreateShortcutEnabled = false;
        try
        {
            var shortcutName = Lang.Text("Tools.Test.Shortcut.FileName", ".lnk");
            var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
            var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");
            var desktop = Paths.GetSpecialPath(Environment.SpecialFolder.Desktop, shortcutName);
            var start = Paths.GetSpecialPath(Environment.SpecialFolder.StartMenu, @"Programs\" + shortcutName);
            var choice = (await Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Tools.Test.Shortcut.SelectLocation"),
                Content = Lang.Text("Tools.Test.Shortcut.ConfirmMessage", desktopName, desktop, startName, start),
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Common.Action.Cancel")),
                    DialogButton.Create(desktopName),
                    DialogButton.Create(startName))
            })).Button;
            if (choice == 1)
                return;
            var shortcutPath = choice == 2 ? desktop : start;
            var locationName = choice == 2 ? desktopName : startName;
            Files.CreateShortcut(shortcutPath, Basics.ExecutablePath);
            HintService.Hint(Lang.Text("Tools.Test.Shortcut.Created", locationName), HintType.Success);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "创建快捷方式失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
        finally
        {
            CreateShortcutEnabled = true;
        }
    }

    [RelayCommand]
    private async Task ShowLaunchCountAsync()
    {
        LaunchCountEnabled = false;
        try
        {
            await Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Tools.Test.LaunchCount.Title"),
                Content = Lang.Text("Tools.Test.LaunchCount.Message", States.System.LaunchCount),
                Buttons = DialogButtons.Ok
            });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "显示启动次数弹窗失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
        finally
        {
            LaunchCountEnabled = true;
        }
    }
}
