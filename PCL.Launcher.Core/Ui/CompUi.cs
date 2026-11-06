namespace PCL;

/// <summary>Comp 业务向 UI 层发出的交互。</summary>
public interface ICompUi
{
    int? ShowSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null);

    List<int>? ShowMultiSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null);

    string? ShowInput(string title, string text = "", string defaultInput = "");
    void RefreshDownloadButton();
    void LoaderStateChangedHintOnly(Loading.LoaderBase loader);

    int ShowDialog(
        string caption,
        string? title,
        string? button1,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false);
}

public static class CompUi
{
    public static ICompUi? Current { get; set; }
}