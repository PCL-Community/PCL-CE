namespace PCL.Core.Platform;

/// <summary>平台快捷方式创建。</summary>
public interface IShortcutService
{
    string GetPreferredExtension(string target);
    string GetDesktopPath(string fileName);
    string GetStartMenuPath(string fileName);

    void Create(
        string shortcutPath,
        string target,
        string? arguments = null,
        string? workingDirectory = null,
        string? description = null,
        string? icon = null);
}