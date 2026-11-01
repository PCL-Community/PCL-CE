using System;
using System.IO;

namespace PCL.Core.Utils.OS;

public static class SystemPaths
{
    /// <summary>
    /// 系统盘符：Windows 上形如 "C:\"，其他平台为 "/"。
    /// </summary>
    public static string DriveLetter { get; } =
        OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";
}