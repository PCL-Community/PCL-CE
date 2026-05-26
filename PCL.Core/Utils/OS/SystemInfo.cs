using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PCL.Core.Utils.OS;

public static class SystemInfo
{
    /// <summary>
    /// 是否为 32 位系统。
    /// </summary>
    public static bool Is32BitSystem = !Environment.Is64BitOperatingSystem;

    /// <summary>
    /// 是否为 ARM64 架构。
    /// </summary>
    public static bool IsArm64System = RuntimeInformation.OSArchitecture == Architecture.Arm64;

    /// <summary>
    /// 是否使用 GBK 编码。
    /// </summary>
    public static bool IsGBKEncoding = Encoding.Default.CodePage == 936;
}