using System.IO;
using System.Text;

namespace PCL;

public static partial class ModBase
{
    public static readonly string ExePath = LauncherPaths.ExecutableDirectory;
    public static string OsDrive = LauncherPaths.SystemDrive;
    public static string PathTemp = LauncherPaths.TempDirectory;
    public static string PathHelpFolder = LauncherPaths.HelpDirectory;

    public static IEnumerable<FileInfo> EnumerateFiles(string Directory) => LauncherFileSystem.EnumerateFiles(Directory);
    public static string ReadFile(string FilePath, Encoding Encoding = null) => LauncherFileSystem.ReadFile(FilePath, Encoding);
    public static bool WriteFile(string FilePath, Stream Stream) => LauncherFileSystem.WriteFile(FilePath, Stream);
    public static bool WriteFile(string FilePath, byte[] Content, bool Append = false) => LauncherFileSystem.WriteFile(FilePath, Content, Append);
    public static bool WriteFile(string FilePath, string Text, bool Append = false, Encoding? Encoding = null) => LauncherFileSystem.WriteFile(FilePath, Text, Append, Encoding);
    public static void ExtractFile(string CompressFilePath, string DestDirectory, Encoding Encode = null,
        Action<double> ProgressIncrementHandler = null) =>
        LauncherFileSystem.ExtractFile(CompressFilePath, DestDirectory, Encode, ProgressIncrementHandler);
    public static int DeleteDirectory(string Path, bool IgnoreIssue = false) => LauncherFileSystem.DeleteDirectory(Path, IgnoreIssue);
    public static string ShortenPath(string LongPath, int ShortenThreshold = 247) => LauncherPaths.ShortenPath(LongPath, ShortenThreshold);
    public static Stream GetResourceStream(string path) => LauncherPaths.GetResourceStream(path);
    public static bool CheckPermission(string Path) => LauncherFileSystem.CheckPermission(Path);
    public static void CheckPermissionWithException(string Path) => LauncherFileSystem.CheckPermissionWithException(Path);
}
