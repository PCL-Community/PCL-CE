namespace PCL;

/// <summary>本地资源加载与 UI 的交互。</summary>
public interface ICompLocalUi
{
    void OnJijResolved();
    bool IsUpdating(string compPath);
    void RefreshModList();
    void SetLoadProgress(object? page, bool visible);
    void SetLoadText(object? page, string key);
    void UpdateOnly(object? page);
    string? GetCurrentFolderPath(object? page);
}

public static class CompLocalUi
{
    public static ICompLocalUi? Current { get; set; }
}