using System;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包处理过程中的异常基类。
/// </summary>
public class ModpackException : Exception
{
    public ModpackException(string message) : base(message) { }
    public ModpackException(string message, Exception? inner) : base(message, inner) { }
}

/// <summary>
/// 压缩包本身无法读取 —— 文件损坏、被加密，或不是受支持的归档格式。
/// </summary>
public sealed class ModpackArchiveException : ModpackException
{
    /// <summary>整合包文件的绝对路径。</summary>
    public string FilePath { get; }

    /// <summary>压缩包是否因为存在加密条目而无法读取。</summary>
    public bool IsEncrypted { get; }

    public ModpackArchiveException(string filePath, string message, bool isEncrypted = false, Exception? inner = null)
        : base(message, inner)
    {
        FilePath = filePath;
        IsEncrypted = isEncrypted;
    }
}

/// <summary>
/// 所有 Provider 均未能识别该压缩包的格式。
/// </summary>
public sealed class ModpackFormatNotRecognizedException(string filePath)
    : ModpackException($"未能识别整合包格式：{filePath}")
{
    /// <summary>整合包文件的绝对路径。</summary>
    public string FilePath { get; } = filePath;
}

/// <summary>
/// 特征文件存在但内容不合法 —— 缺少必填字段、字段类型错误或 JSON 解析失败。
/// </summary>
public sealed class ModpackManifestInvalidException : ModpackException
{
    /// <summary>抛出该异常的格式。</summary>
    public ModpackFormat Format { get; }

    /// <summary>压缩包内的清单文件路径。</summary>
    public string ManifestPath { get; }

    public ModpackManifestInvalidException(
        ModpackFormat format, string manifestPath, string message, Exception? inner = null)
        : base($"{format.ToDisplayName()} 整合包清单不合法（{manifestPath}）：{message}", inner)
    {
        Format = format;
        ManifestPath = manifestPath;
    }
}

/// <summary>
/// 整合包声明了当前启动器不支持的内容（例如已停止支持的加载器）。
/// </summary>
public sealed class ModpackUnsupportedContentException(string message) : ModpackException(message);

/// <summary>
/// 整合包中的文件路径越出了实例目录 —— 典型的 Zip Slip 攻击或整合包制作错误。
/// </summary>
public sealed class ModpackUnsafePathException(string offendingPath)
    : ModpackException($"整合包中的文件路径越出实例目录：{offendingPath}")
{
    /// <summary>被拒绝的原始路径。</summary>
    public string OffendingPath { get; } = offendingPath;
}
