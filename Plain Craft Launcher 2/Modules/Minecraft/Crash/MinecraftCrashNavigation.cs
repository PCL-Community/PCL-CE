namespace PCL;

public static class MinecraftCrashNavigation
{
    public static void NavigateTo(MinecraftCrashSession session,
        FormMain.PageSubType tab = FormMain.PageSubType.CrashOverview)
    {
        MinecraftCrashSessionStore.SetCurrent(session);
        ModBase.RunInUi(() =>
        {
            ModMain.frmMain?.PageChange(FormMain.PageType.CrashAnalysis, tab);
            if (ModMain.frmMain?.pageRight is IRefreshable refreshable)
                refreshable.Refresh();
        });
    }
}