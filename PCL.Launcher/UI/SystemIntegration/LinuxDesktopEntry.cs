using System.Text;
using PCL.Core.Logging;

namespace PCL.UI.SystemIntegration;

/// <summary>
///     Wayland 没有传统的窗口图标协议，桌面环境通过 app_id 查找
///     XDG desktop entry 及其 Icon。这里在原生 Wayland 启动前确保
///     desktop entry 和图标文件存在，避免任务栏显示 Wayland 占位图标。
/// </summary>
public static class LinuxDesktopEntry
{
    public const string AppId = "pcl-community-edition";

    public static void Ensure()
    {
        try
        {
            var executable = Environment.ProcessPath;
            var iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Images",
                "icon.png");
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(iconPath))
                return;

            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                dataHome = Path.Combine(home, ".local", "share");
            }

            var applications = Path.Combine(dataHome, "applications");
            Directory.CreateDirectory(applications);

            var desktopPath = Path.Combine(applications, $"{AppId}.desktop");
            var content = new StringBuilder()
                .AppendLine("[Desktop Entry]")
                .AppendLine("Type=Application")
                .AppendLine("Name=PCL Community Edition")
                .AppendLine("Name[zh_CN]=PCL 社区版")
                .AppendLine("Name[zh_TW]=PCL 社群版")
                .AppendLine("Comment=PCL Community Edition launcher")
                .AppendLine($"Exec={QuoteDesktopArg(executable)}")
                .AppendLine($"Icon={EscapeDesktopValue(iconPath)}")
                .AppendLine($"StartupWMClass={AppId}")
                .AppendLine("Terminal=false")
                .AppendLine("Categories=Game;Utility;")
                .AppendLine("StartupNotify=true")
                .ToString();

            File.WriteAllText(desktopPath, content, new UTF8Encoding(false));
            LogWrapper.Debug("UI", $"Wayland desktop entry 已更新：{desktopPath}");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "创建 Wayland desktop entry 失败");
        }
    }

    private static string QuoteDesktopArg(string value)
    {
        return "\""
            + value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
            + "\"";
    }

    private static string EscapeDesktopValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
    }
}