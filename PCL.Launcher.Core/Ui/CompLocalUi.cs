namespace PCL;

/// <summary>实例本地资源页向业务层公开的页面契约，不是 UI 平台桥。</summary>
public interface ICompLocalPage
{
    bool LoadProgressVisible { get; set; }

    string? CurrentFolderPath { get; }

    void SetLoadText(string key);

    void UpdateOnly();

    void RefreshModList();
}

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