using PCL.Navigation;

namespace PCL.UI.Bridges;

/// <summary>本地资源（Comp）加载与页面状态的桥接。</summary>
public sealed class CompLocalUiBridge : ICompLocalUi
{
    public void OnJijResolved()
    {
        LauncherCommon.RunInUi(() =>
        {
            if (PageRouter.CurrentPage is ICompLocalPage page) page.RefreshModList();
            else UiInteraction.WarnMissing(nameof(OnJijResolved), "COMPLOCAL");
        });
    }

    public bool IsUpdating(string compPath)
    {
        return CompLocalUpdateRegistry.IsUpdating(compPath);
    }

    public void RefreshModList()
    {
        LauncherCommon.RunInUi(() =>
        {
            if (PageRouter.CurrentPage is ICompLocalPage page) page.RefreshModList();
            else UiInteraction.WarnMissing(nameof(RefreshModList), "COMPLOCAL");
        });
    }

    public void SetLoadProgress(object? page, bool visible)
    {
        if (page is not ICompLocalPage localPage)
        {
            UiInteraction.WarnMissing(nameof(SetLoadProgress), "COMPLOCAL");
            return;
        }

        UiInteraction.RunOnUi(() => localPage.LoadProgressVisible = visible);
    }

    public void SetLoadText(object? page, string key)
    {
        if (page is not ICompLocalPage localPage)
        {
            UiInteraction.WarnMissing(nameof(SetLoadText), "COMPLOCAL");
            return;
        }

        UiInteraction.RunOnUi(() => localPage.SetLoadText(key));
    }

    public void UpdateOnly(object? page)
    {
        if (page is not ICompLocalPage localPage)
        {
            UiInteraction.WarnMissing(nameof(UpdateOnly), "COMPLOCAL");
            return;
        }

        UiInteraction.RunOnUi(localPage.UpdateOnly);
    }

    public string? GetCurrentFolderPath(object? page)
    {
        if (page is ICompLocalPage localPage) return localPage.CurrentFolderPath;
        UiInteraction.WarnMissing(nameof(GetCurrentFolderPath), "COMPLOCAL");
        return null;
    }
}