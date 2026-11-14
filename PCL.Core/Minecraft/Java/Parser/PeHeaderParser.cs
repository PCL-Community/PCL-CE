using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Parser;

/// Windows exe file binary. Get version and brand info from PE header.
public class PeHeaderParser : IJavaParser
{
    /// Do not modify the map unless you have tested the real binary section.
    private static readonly Dictionary<string, JavaBrandType> _BrandMap = new()
    {
        ["Eclipse"] = JavaBrandType.EclipseTemurin,
        ["Temurin"] = JavaBrandType.EclipseTemurin,
        ["Bellsoft"] = JavaBrandType.Liberica,
        ["Microsoft"] = JavaBrandType.Microsoft,
        ["Amazon"] = JavaBrandType.Corretto,
        ["Azul"] = JavaBrandType.Zulu,
        ["IBM"] = JavaBrandType.IBMSemeru,
        ["Oracle"] = JavaBrandType.Oracle,
        ["Tencent"] = JavaBrandType.TencentKona,
        ["OpenJDK"] = JavaBrandType.OpenJDK,
        ["Alibaba"] = JavaBrandType.Dragonwell,
        ["GraalVM"] = JavaBrandType.GraalVmCommunity,
        ["JetBrains"] = JavaBrandType.JetBrains
    };

    public Result<JavaInstallation, Exception> Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
                return Result<JavaInstallation, Exception>
                    .Err(new FileNotFoundException($"{javaExePath} is not found"));

            var versionInfo = FileVersionInfo.GetVersionInfo(javaExePath);
            // The JDK defines only the meaning of the first four segments of the version number,
            // and subsequent fields are defined independently by vendors.
            var versionSection = versionInfo.FileVersion is null
                ? "0.0.0.0"
                : String.Join('.', values: versionInfo.FileVersion
                    .Split('.')
                    .Take(4));
            var fileVersion = Version.Parse(versionSection);
            var companyName = _NormalizeCompanyName(versionInfo);
            var brand = _DetermineBrand(companyName);

            var javaFolder = Path.GetDirectoryName(javaExePath)!;
            // Jre is pure runtime and do not have javac binary.
            var isJre = !File.Exists(Path.Combine(javaFolder, "javac.exe"));

            var peData = PEHeaderReader.ReadPEHeader(javaExePath);
            var arch = peData.Machine;
            var is64Bit = PEHeaderReader.IsMachine64Bit(arch);

            return Result<JavaInstallation, Exception>
                .Ok(new JavaInstallation(
                    javaFolder,
                    fileVersion,
                    brand,
                    arch,
                    is64Bit,
                    isJre
                ));
        }
        catch (Exception ex)
        {
            return Result<JavaInstallation, Exception>
                .Err(ex);
        }
    }

    private static string _NormalizeCompanyName(FileVersionInfo info)
    {
        var name = info.CompanyName ?? info.FileDescription ?? info.ProductName ?? string.Empty;

        // Fix the issue about confusion on Oracle/OpenJDK binary
        if (name.Contains("Oracle", StringComparison.OrdinalIgnoreCase) || name == "N/A")
        {
            if ((info.FileDescription?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (info.ProductName?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) ?? false))
                return "Oracle";
            return "OpenJDK";
        }
        return name;
    }

    private static JavaBrandType _DetermineBrand(string output)
    {
        var match = _BrandMap.Keys
            .FirstOrDefault(k => output.Contains(k, StringComparison.OrdinalIgnoreCase));
        return match is not null ? _BrandMap[match] : JavaBrandType.Unknown;
    }
}
