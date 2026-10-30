namespace PCL;

public partial class Setup
{
    public static void VersionRamType(int type)
    {
        if (Launcher.frmInstanceSetup is null)
            return;
        Launcher.frmInstanceSetup.RamType(type);
    }

    public static void VersionServerLogin(int type)
    {
        if (Launcher.frmInstanceSetup is null)
            return;

        Common.WriteIni($"{GameFolders.mcFolderSelected}PCL.ini", "InstanceCache", "");
        if (PageInstanceLeft.mcInstance is null || GameFolders.mcFolderSelected is null)
            return;

        PageInstanceLeft.mcInstance = new McInstance(PageInstanceLeft.mcInstance.Name).Load();
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected,
            Loading.LoaderFolderRunType.ForceRun,
            1,
            @"versions\");
    }
}
