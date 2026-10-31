namespace PCL;

/// <summary>整合包安装与 UI 的交互。</summary>
public interface IModpackUi
{
    List<Loading.LoaderBase> CreateInstallLoader(object request);
    void OnInstallStateChanged(object loader);
    void AddFolder(string folderPath, string displayName, bool showHint);
}

public static class ModpackUi
{
    public static IModpackUi? Current { get; set; }

    public static List<Loading.LoaderBase> CreateInstallLoader(object request)
    {
        return Current?.CreateInstallLoader(request) ?? [];
    }

    public static void OnInstallStateChanged(object loader)
    {
        Current?.OnInstallStateChanged(loader);
    }

    public static void AddFolder(string path, string name, bool showHint)
    {
        Current?.AddFolder(path, name, showHint);
    }
}