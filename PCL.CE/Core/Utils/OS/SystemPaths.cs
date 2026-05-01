using System;
using System.IO;

namespace PCL.CE.Core.Utils.OS;

public static class SystemPaths {
    /// <summary>
    /// 系统盘符（含冒号和反斜杠），例如 "C:\"。
    /// </summary>
    public static string DriveLetter { get; } = Path.GetPathRoot(Environment.SystemDirectory)!;
}
