using Microsoft.Win32;

namespace PCL.Core.Utils.OS;

public static class AumidHelper
{
    public static bool HasAumid()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\AppUserModelId\XXX");
        return key != null;
    }
    
    public static void RegisterAumid(string aumid)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{aumid}");
        key.SetValue("DisplayName", "Plain Craft Launcher Community Edition");
        key.SetValue("IconUri", IconHelper.GetIconPath());
    }

    public static void UnregisterAumid(string aumid)
    {
        Registry.CurrentUser.DeleteSubKey($@"Software\Classes\AppUserModelId\{aumid}", false);
    }
}