using PCL.Core.Utils;
using System;
using System.IO;

namespace PCL.Core.Minecraft.Java;

public sealed record JavaInstallation(
    string JavaFolder,
    Version Version,
    JavaBrandType Brand,
    MachineType Architecture,
    bool Is64Bit,
    bool IsJre)
{
    public string JavaExePath => Path.Combine(JavaFolder, "java.exe");
    public string? JavawExePath
    {
        get
        {
            var javaw = Path.Combine(JavaFolder, "javaw.exe");
            return File.Exists(javaw) ? javaw : null;
        }
    }

    /// <summary>
    /// Java 主版本号
    /// </summary>
    public int MajorVersion => Version.Major == 1 ? Version.Minor : Version.Major;

    /// <summary>
    /// Fast check for Java availability.
    /// This DO NOT mean the Java works well.
    /// </summary>
    public bool IsStillAvailable => File.Exists(JavaExePath);

    public override string ToString() =>
        $"{(IsJre ? "JRE" : "JDK")} {MajorVersion} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaFolder}";

    public string ToDetailedString() =>
        $"{(IsJre ? "JRE" : "JDK")} {Version} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaFolder}";
}
