using Microsoft.Win32;

namespace PCL.Core.Utils.OS;

public static class AumidHelper
{
    // TODO: 针对每个路径建立单独的 AUMID
    
    public static bool HasAumid()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\AppUserModelId\PCLCommunity.PCLCE");
        return key is not null;
    }
    
    public static void RegisterAumid()
    {
        //
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\PCLCommunity.PCLCE");
        key.SetValue("DisplayName", "Plain Craft Launcher Community Edition");
        key.SetValue("IconUri", IconHelper.GetIconPath());
        key.SetValue("IconBackgroundColor", "FFDDDD");
    }

    public static void UnregisterAumid()
    {
        Registry.CurrentUser.DeleteSubKey($@"Software\Classes\AppUserModelId\PCLCommunity.PCLCE", false);
    }
}