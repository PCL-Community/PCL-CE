using PCL.Core.Platform;

namespace PCL;

/// <summary>将 Comp 交互桥接到 WPF 对话框。</summary>
public sealed class WpfCompUi : ICompUi
{
    public int? ShowSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null)
    {
        return DialogCompat.ShowSelect(caption, title, options, confirmText, cancelText);
    }

    public List<int>? ShowMultiSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null)
    {
        return DialogCompat.ShowMultiSelect(caption, title, options, confirmText, cancelText);
    }

    public string? ShowInput(string title, string text = "", string defaultInput = "")
    {
        return DialogCompat.ShowInput(title, text, defaultInput);
    }

    public void RefreshDownloadButton()
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        });
    }

    public void LoaderStateChangedHintOnly(Loading.LoaderBase loader)
    {
        DownloadUi.LoaderStateChangedHintOnly(loader);
    }

    public int ShowDialog(
        string caption,
        string? title,
        string? button1,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false)
    {
        return DialogCompat.ShowMessageBox(
            caption,
            title,
            button1,
            button2,
            button3,
            isWarn,
            forceWait: true);
    }
}