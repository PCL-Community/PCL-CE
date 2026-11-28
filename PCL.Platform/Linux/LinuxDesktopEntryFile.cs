using System.Text;

namespace PCL.Platform;

/// <summary>生成 freedesktop desktop entry 文件内容。</summary>
public static class LinuxDesktopEntryFile
{
    public const string PclCeAppId = "pcl-community-edition";

    public static string Build(
        string name,
        string? comment,
        string target,
        string? arguments = null,
        string? workingDirectory = null,
        string? icon = null,
        string? startupWmClass = null,
        string categories = "Game;Utility;")
    {
        var builder = new StringBuilder();
        builder.AppendLine("[Desktop Entry]");
        builder.AppendLine("Version=1.0");
        builder.AppendLine("Type=Application");
        builder.Append("Name=").AppendLine(EscapeValue(name));
        if (!string.IsNullOrWhiteSpace(comment))
            builder.Append("Comment=").AppendLine(EscapeValue(comment));
        builder.Append("Exec=").AppendLine(EscapeValue(BuildExec(target, arguments)));
        builder.Append("TryExec=").AppendLine(EscapeValue(target));
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            builder.Append("Path=").AppendLine(EscapeValue(workingDirectory));
        if (!string.IsNullOrWhiteSpace(icon))
            builder.Append("Icon=").AppendLine(EscapeValue(icon));
        builder.AppendLine("Terminal=false");
        builder.Append("Categories=").AppendLine(EscapeValue(categories));
        if (!string.IsNullOrWhiteSpace(startupWmClass))
            builder.Append("StartupWMClass=").AppendLine(EscapeValue(startupWmClass));
        builder.AppendLine("StartupNotify=true");
        return builder.ToString();
    }

    public static string BuildExec(string target, string? arguments)
    {
        var escaped = target
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(arguments)
            ? $"\"{escaped}\""
            : $"\"{escaped}\" {arguments}";
    }

    public static string EscapeValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}