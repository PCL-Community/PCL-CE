using PCL.Core.Platform;

namespace PCL;

/// <summary>本地资源 UI 交互桥接。</summary>
public sealed class WpfCompLocalUi : ICompLocalUi
{
    public void OnJijResolved()
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmInstanceModJarInJar?.OnJijResolved());
    }

    public bool IsUpdating(string compPath)
    {
        return PageInstanceCompResource.updatingVersions.Contains(compPath);
    }

    public void RefreshModList()
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmInstanceMod?.Filter == PageInstanceCompResource.FilterType.CanUpdate)
                Launcher.frmInstanceMod.RefreshUI();
            else
                Launcher.frmInstanceMod?.RefreshBars();
        });
    }

    public void SetLoadProgress(object? page, bool visible)
    {
        PlatformServices.UiDispatcher?.Invoke(() =>
        {
            if (page is PageInstanceCompResource { Load: { } load })
                load.ShowProgress = visible;
        });
    }

    public void SetLoadText(object? page, string key)
    {
        PlatformServices.UiDispatcher?.Invoke(() =>
        {
            if (page is PageInstanceCompResource { Load: { } load })
                load.TrySetLocalizedValue("Text", key);
        });
    }

    public void UpdateOnly(object? page)
    {
        PlatformServices.UiDispatcher?.Invoke(() =>
        {
            if (page is PageInstanceCompResource resource)
                resource.LoaderRun(Loading.LoaderFolderRunType.UpdateOnly);
        });
    }

    public string? GetCurrentFolderPath(object? page)
    {
        if (page is not PageInstanceCompResource resource) return null;
        return PlatformServices.UiDispatcher?.Invoke(() => resource.CurrentFolderPath);
    }
}