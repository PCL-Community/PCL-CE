using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Parser;
public class PeHeaderParser : IJavaParser
{
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

    public JavaInstallation? Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
                return null;

            LogWrapper.Info("Java", $"解析 {javaExePath} 的 Java 程序信息");

            var versionInfo = FileVersionInfo.GetVersionInfo(javaExePath);
            var fileVersion = Version.Parse(versionInfo.FileVersion ?? "0.0.0.0");
            var companyName = _NormalizeCompanyName(versionInfo);
            var brand = _DetermineBrand(companyName);

            var javaFolder = Path.GetDirectoryName(javaExePath)!;
            var isJre = !File.Exists(Path.Combine(javaFolder, "javac.exe"));

            var peData = PEHeaderReader.ReadPEHeader(javaExePath);
            var arch = peData.Machine;
            var is64Bit = PEHeaderReader.IsMachine64Bit(arch);

            // 可用性检查（不影响模型创建，由调用方决定是否启用）
            var libDir = Path.Combine(Directory.GetParent(javaFolder)!.FullName, "lib");
            var isUsable = (!isJre && File.Exists(Path.Combine(libDir, "jvm.lib"))) ||
                           (isJre && File.Exists(Path.Combine(libDir, "rt.jar")));

            return new JavaInstallation(
                javaFolder,
                fileVersion,
                brand,
                arch,
                is64Bit,
                isJre
            );
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[Java] 解析 {javaExePath} 时出错");
            return null;
        }
    }

    private static string _NormalizeCompanyName(FileVersionInfo info)
    {
        var name = info.CompanyName ?? info.FileDescription ?? info.ProductName ?? string.Empty;

        // 修复 Oracle/OpenJDK 混淆问题
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
        return match != null ? _BrandMap[match] : JavaBrandType.Unknown;
    }
}
