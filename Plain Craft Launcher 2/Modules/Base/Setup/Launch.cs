namespace PCL;

public partial class Setup
{
    public static void LaunchInstanceSelect(string value)
    {
        Common.Log($"[Setup] 当前选择的 Minecraft 版本：{value}");
        Common.WriteIni($"{GameFolders.mcFolderSelected}PCL.ini", "Version", value);
    }

    public static void LaunchFolderSelect(string value)
    {
        Common.Log($"[Setup] 当前选择的 Minecraft 文件夹：{value.Replace("$", Common.ExePath)}");
        GameFolders.mcFolderSelected = value.Replace("$", Common.ExePath);
    }

    public static void LaunchRamType(int type)
    {
        if (Launcher.frmSetupLaunch is null)
            return;
        Launcher.frmSetupLaunch.RamType(type);
    }
}
