namespace PCL.Core.Platform;

public interface IShellLauncher
{
    void OpenPath(string path, string? workingDirectory = null, string? arguments = null);
    void OpenUrl(string url);
}