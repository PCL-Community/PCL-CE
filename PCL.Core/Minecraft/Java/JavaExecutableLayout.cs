using System.IO;
using PCL.Core.Platform;

namespace PCL.Core.Minecraft.Java;

/// <summary>
///     Java 可执行文件的平台布局：Windows 使用 java.exe / javaw.exe，其余平台使用 java。
/// </summary>
public static class JavaExecutableLayout
{
    public static string JavaExecutableName => PlatformRuntime.Current.Java.JavaExecutableName;

    public static string? JavawExecutableName => PlatformRuntime.Current.Java.JavawExecutableName;

    public static string GetJavaExePath(string javaFolder)
    {
        return Path.Combine(javaFolder, JavaExecutableName);
    }

    public static string? GetJavawExePath(string javaFolder)
    {
        return JavawExecutableName is { } javawName
            ? Path.Combine(javaFolder, javawName)
            : null;
    }
}