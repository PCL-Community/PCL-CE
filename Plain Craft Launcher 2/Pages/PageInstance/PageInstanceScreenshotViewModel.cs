using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例截图页的视图模型。</summary>
public partial class PageInstanceScreenshotViewModel : ViewModelBase
{
    [ObservableProperty] private bool _showContent = true;

    private string ScreenshotPath => PageInstanceLeft.McInstance.PathIndie + @"screenshots\";

    public void RefreshTip(int fileCount)
    {
        ShowContent = fileCount != 0;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var path = ScreenshotPath;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        Common.OpenExplorer(path);
    }

    [RelayCommand]
    private void OpenScreenshot(string? path)
    {
        if (path is null)
            return;
        Common.OpenExplorer(path);
    }

    [RelayCommand]
    private void CopyScreenshot(string? path)
    {
        if (path is null)
            return;
        if (File.Exists(path))
        {
            var tryTime = 0;
            while (tryTime <= 5)
                try
                {
                    Common.Log("[Screenshot] 尝试复制" + path + "到剪贴板");
                    Clipboard.SetImage(new BitmapImage(new Uri(path)));
                    HintService.Show(Hints.Info(Lang.Text("Instance.Screenshot.CopiedToClipboard")));
                    tryTime = 6;
                    return;
                }
                catch (Exception ex)
                {
                    tryTime += 1;
                    Common.Log(ex, $"[Screenshot]第 {tryTime} 次复制尝试失败");
                }

            HintService.Show(Hints.Error(Lang.Text("Instance.Screenshot.CopyFailed")));
        }
        else
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Screenshot.FileNotFound")));
        }
    }
}
