using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;

/// <summary>Java 安装解析的公共入口与品牌/版本工具，具体二进制读取由平台实现负责。</summary>
public abstract partial class JavaParserBase : IJavaParser
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

    public Result<JavaInstallation, Exception> Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
                return Result<JavaInstallation, Exception>.Err(
                    new FileNotFoundException($"Java executable not found: {javaExePath}", javaExePath));

            LogWrapper.Info("Java", $"解析 {javaExePath} 的 Java 程序信息");
            var installation = ParseCore(javaExePath);
            return installation is null
                ? Result<JavaInstallation, Exception>.Err(
                    new InvalidDataException($"Unable to parse Java executable: {javaExePath}"))
                : Result<JavaInstallation, Exception>.Ok(installation);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[Java] 解析 {javaExePath} 时出错");
            return Result<JavaInstallation, Exception>.Err(ex);
        }
    }

    protected abstract JavaInstallation? ParseCore(string javaExePath);

    [GeneratedRegex(@"\d+(?:[._]\d+)*")]
    protected static partial Regex VersionPattern();

    protected static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var main = value.Trim().Trim('"').Split('+', '-')[0].Replace('_', '.');
        var numbers = new List<int>(4);
        foreach (var part in main.Split('.'))
        {
            if (numbers.Count == 4 || !int.TryParse(part, out var number))
                break;
            numbers.Add(number);
        }

        if (numbers.Count == 0)
            return null;

        return new Version(
            numbers[0],
            numbers.Count > 1 ? numbers[1] : 0,
            numbers.Count > 2 ? numbers[2] : 0,
            numbers.Count > 3 ? numbers[3] : 0);
    }

    protected static JavaBrandType DetermineBrand(string output)
    {
        var match = _BrandMap.Keys
            .FirstOrDefault(key => output.Contains(key, StringComparison.OrdinalIgnoreCase));
        return match is not null ? _BrandMap[match] : JavaBrandType.Unknown;
    }
}