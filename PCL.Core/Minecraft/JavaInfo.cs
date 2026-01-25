using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft;

// ReSharper disable IdentifierTypo, InconsistentNaming

public enum JavaBrandType
{
    EclipseTemurin,
    Bellsoft,
    AzulZulu,
    AmazonCorretto,
    Microsoft,
    IBMSemeru,
    Oracle,
    Dragonwell,
    TencentKona,
    OpenJDK,
    GraalVmCommunity,
    JetBrains,
    Unknown
}

[Serializable]
public class JavaInfo(string javaFolder, Version version, JavaBrandType brand, bool isEnabled, MachineType arch, bool is64Bit, bool isJre)
{
    /// <summary>
    /// 就像这样：
    /// D:\Program Files\Java24\bin
    /// </summary>
    public string JavaFolder => javaFolder;

    public Version Version => version;
    
    public int JavaMajorVersion => Version.Major == 1
        ? Version.Minor
        : Version.Major;

    public JavaBrandType Brand => brand;

    /// <summary>
    /// 用户是否启动此 Java
    /// </summary>
    public bool IsEnabled { get; set; } = isEnabled;

    public MachineType JavaArch => arch;

    public bool Is64Bit => is64Bit;

    public bool IsJre => isJre;
    
    public string JavaExePath => Path.Combine(JavaFolder, "java.exe");
    
    public string JavawExePath => Path.Combine(JavaFolder, "javaw.exe");

    public override string ToString()
    {
        return $" {(IsJre ? "JRE" : "JDK")} {JavaMajorVersion} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaFolder}";
    }

    public string ToString(bool detailed)
    {
        if (!detailed)
            return ToString();
        return $" {(IsJre ? "JRE" : "JDK")} {Version} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaFolder}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is JavaInfo model)
        {
            return JavaFolder.Equals(model.JavaFolder, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return JavaFolder.GetHashCode();
    }

    public bool IsStillAvailable => File.Exists(JavaExePath);

    /// <summary>
    /// 通过路径获取 Java 实例化信息，如果 Java 信息出现错误返回 null
    /// </summary>
    /// <param name="javaExePath">java.exe 的文件地址</param>
    /// <returns></returns>
    public static JavaInfo? Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
                return null;
            LogWrapper.Info("Java", $"解析 {javaExePath} 的 Java 程序信息");
            var javaFileVersion = FileVersionInfo.GetVersionInfo(javaExePath);
            var javaVersion = Version.Parse(javaFileVersion.FileVersion!);
            var companyName = javaFileVersion.CompanyName
                              ?? javaFileVersion.FileDescription
                              ?? javaFileVersion.ProductName
                              ?? string.Empty;
            // 某 O 开头的公司乱写文件属性
            if (companyName.Contains("Oracle") || companyName == "N/A")
            {
                if (javaFileVersion.FileDescription?.Contains("Java(TM)") ?? javaFileVersion.ProductName?.Contains("Java(TM)") ?? false)
                    companyName = "Oracle";
                else
                    companyName = "OpenJDK";
            }
            
            var javaBrand = DetermineBrand(companyName);

            var currentJavaFolder = Path.GetDirectoryName(javaExePath)!;
            var isJavaJre = !File.Exists(Path.Combine(currentJavaFolder, "javac.exe"));
            var peData = PEHeaderReader.ReadPEHeader(javaExePath);
            var currentJavaArch = peData.Machine;
            var isJava64Bit = PEHeaderReader.IsMachine64Bit(peData.Machine);
            var javaLibDir = Path.Combine(Directory.GetParent(currentJavaFolder)!.FullName, "lib");
            var isJavaUsable = (!isJavaJre && File.Exists(Path.Combine(javaLibDir, "jvm.lib"))) ||
                               (isJavaJre && File.Exists(Path.Combine(javaLibDir, "rt.jar")));
            var shouldDisableByDefault =
                (isJavaJre && javaVersion.Major > 8)
                || (isJava64Bit ^ Environment.Is64BitOperatingSystem)
                || !isJavaUsable;

            return new JavaInfo(
                currentJavaFolder,
                javaVersion,
                javaBrand,
                !shouldDisableByDefault,
                currentJavaArch,
                isJava64Bit,
                isJavaJre
            );
        }
        catch(Exception e)
        {
            LogWrapper.Error(e, $"[Java] 解析 {javaExePath} 的信息时出现错误");
        }
        return null;
    }
    
    private static readonly Dictionary<string, JavaBrandType> _brandMap = new()
    {
        ["Eclipse"] = JavaBrandType.EclipseTemurin,
        ["Temurin"] = JavaBrandType.EclipseTemurin,
        ["Bellsoft"] = JavaBrandType.Bellsoft,
        ["Microsoft"] = JavaBrandType.Microsoft,
        ["Amazon"] = JavaBrandType.AmazonCorretto,
        ["Azul"] = JavaBrandType.AzulZulu,
        ["IBM"] = JavaBrandType.IBMSemeru,
        ["Oracle"] = JavaBrandType.Oracle,
        ["Tencent"] = JavaBrandType.TencentKona,
        ["OpenJDK"] = JavaBrandType.OpenJDK,
        ["Alibaba"] = JavaBrandType.Dragonwell,
        ["GraalVM"] = JavaBrandType.GraalVmCommunity,
        ["JetBrains"] = JavaBrandType.JetBrains
    };

    private static JavaBrandType DetermineBrand(string? output)
    {
        if (output == null) return JavaBrandType.Unknown;
        var result = _brandMap.Keys
            .Where(item => output.Contains(item, StringComparison.OrdinalIgnoreCase)).ToList();
        return result.Count != 0
            ? _brandMap[result.First()]
            : JavaBrandType.Unknown;
    }
}
