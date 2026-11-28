using System.Text;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using PCL.Platform;

namespace PCL.UI.SystemIntegration;

/// <summary>
/// Wayland 没有传统的窗口图标协议，桌面环境通过 app_id 查找 XDG desktop entry 及其 Icon。这里在原生 Wayland 启动前确保
/// desktop entry 和图标文件存在，避免任务栏显示 Wayland 占位图标。
/// </summary>
public static class LinuxDesktopEntry
{
    public const string AppId = LinuxDesktopEntryFile.PclCeAppId;

    public static void Ensure()
    {
        try
        {
            if (Paths.PackageKind is "deb" or "rpm" or "appimage") return;

            var executable = Environment.ProcessPath;
            var iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Images",
                "icon.png");
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(iconPath))
                return;

            var applications = Path.Combine(XdgPaths.DataHome, "applications");
            Directory.CreateDirectory(applications);

            var desktopPath = Path.Combine(applications, $"{AppId}.desktop");
            var content = LinuxDesktopEntryFile.Build(
                "PCL Community Edition",
                "Plain Craft Launcher Community Edition",
                executable,
                workingDirectory: AppContext.BaseDirectory,
                icon: iconPath,
                startupWmClass: AppId);

            File.WriteAllText(desktopPath, content, new UTF8Encoding(false));
            LogWrapper.Debug("UI", $"Wayland desktop entry 已更新：{desktopPath}");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "创建 Wayland desktop entry 失败");
        }
    }
}