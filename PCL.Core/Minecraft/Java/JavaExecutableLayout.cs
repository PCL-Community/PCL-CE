using System;
using System.IO;

namespace PCL.Core.Minecraft.Java;

/// <summary>
///     Java 可执行文件的平台布局：Windows 使用 java.exe / javaw.exe，其余平台使用 java。
/// </summary>
public static class JavaExecutableLayout
{
    private static readonly bool _IsWindowsPlatform = OperatingSystem.IsWindows();

    public static string JavaExecutableName => _IsWindowsPlatform ? "java.exe" : "java";

    /// <summary>非 Windows 平台没有 javaw，返回 null。</summary>
    public static string? JavawExecutableName => _IsWindowsPlatform ? "javaw.exe" : null;

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