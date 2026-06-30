using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     PCL2 启动器字体应用入口。
/// </summary>
public static class LauncherFontService
{
    public static void SetLaunchFont(string? fontName = null)
    {
        try
        {
            LocalizationFontService.ApplyLaunchFont(fontName, LocalizationService.CurrentLanguage);
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "设置字体失败", ModBase.LogLevel.Hint);
        }
    }
}