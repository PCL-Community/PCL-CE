using System;
using System.Diagnostics;
using System.IO;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

/// <summary>Windows Java 解析：PE 头、文件版本信息和 javac.exe。</summary>
public sealed class WindowsJavaParser : JavaParserBase
{
    protected override JavaInstallation? ParseCore(string javaExePath)
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(javaExePath);
        var fileVersion = Version.Parse(versionInfo.FileVersion ?? "0.0.0.0");
        var companyName = _NormalizeCompanyName(versionInfo);
        var brand = DetermineBrand(companyName);

        var javaFolder = Path.GetDirectoryName(javaExePath)!;
        var isJre = !File.Exists(Path.Combine(javaFolder, "javac.exe"));

        var peData = PEHeaderReader.ReadPEHeader(javaExePath);
        var arch = peData.Machine;
        var is64Bit = PEHeaderReader.IsMachine64Bit(arch);

        return new JavaInstallation(javaFolder, fileVersion, brand, arch, is64Bit, isJre);
    }

    private static string _NormalizeCompanyName(FileVersionInfo info)
    {
        var name = info.CompanyName ?? info.FileDescription ?? info.ProductName ?? string.Empty;

        if (!name.Contains("Oracle", StringComparison.OrdinalIgnoreCase) && name != "N/A")
            return name;

        if ((info.FileDescription?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase)
                ?? false)
            || (info.ProductName?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase)
                ?? false))
            return "Oracle";
        return "OpenJDK";
    }
}