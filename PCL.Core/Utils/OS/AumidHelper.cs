using System;
using Microsoft.Win32;

namespace PCL.Core.Utils.OS;

public static class AumidHelper
{
    public const string Aumid = "PCLCommunity.PCLCE";

    public static bool HasAumid()
    {
        if (!OperatingSystem.IsWindows()) return false;

        using var key =
            Registry.CurrentUser.OpenSubKey(
                string.Concat(@"Software\Classes\AppUserModelId\", Aumid));
        return key is not null;
    }

    public static void RegisterAumid()
    {
        if (!OperatingSystem.IsWindows()) return;

        // .NET 8 在正常情况下不可能返回 null，如果炸了不应该包住而是让他炸下去
        using var key = Registry.CurrentUser.CreateSubKey(
            string.Concat(@"Software\Classes\AppUserModelId\", Aumid));
        key.SetValue("DisplayName", "Plain Craft Launcher Community Edition");
        key.SetValue("IconUri", IconHelper.GetIconPath());
        key.SetValue("IconBackgroundColor", "FFDDDD");
    }

    public static void UnregisterAumid()
    {
        if (!OperatingSystem.IsWindows()) return;

        Registry.CurrentUser.DeleteSubKey(
            string.Concat(@"Software\Classes\AppUserModelId\", Aumid),
            false);
    }
}