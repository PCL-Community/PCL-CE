using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Modpack.Installation;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack.Persistence;

/// <summary>
/// <c>modpack.json</c> 的读写。
/// </summary>
public static class ModpackConfigurationStore
{
    /// <summary>返回实例目录下记录文件的完整路径。</summary>
    public static string GetPath(string instanceDirectory)
        => Path.Combine(instanceDirectory, ModpackConfiguration.FileName);

    /// <summary>
    /// 读取实例的安装记录。
    /// </summary>
    /// <returns>文件不存在或内容损坏时返回 <c>null</c>。</returns>
    public static ModpackConfiguration? TryRead(string instanceDirectory)
    {
        var path = GetPath(instanceDirectory);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<ModpackConfiguration>(
                File.ReadAllText(path, Encoding.UTF8), JsonCompat.SerializerOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            LogWrapper.Warn("Modpack", $"读取整合包安装记录失败（{path}）：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 写入实例的安装记录。
    /// </summary>
    public static async Task WriteAsync(
        string instanceDirectory,
        ModpackConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(instanceDirectory);
        var path = GetPath(instanceDirectory);

        var json = JsonSerializer.Serialize(configuration, _WriteOptions);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        LogWrapper.Info("Modpack", $"已写入整合包安装记录：{path}");
    }

    /// <summary>
    /// 由安装方案与覆写快照构造安装记录。
    /// </summary>
    public static ModpackConfiguration Create(
        ModpackInstallPlan plan, IReadOnlyList<ModpackFileSnapshot> overrides, System.Text.Json.Nodes.JsonNode? manifest)
        => new()
        {
            Type = plan.Format.ToDisplayName(),
            Name = plan.Metadata.Name,
            Version = plan.Metadata.Version,
            Manifest = manifest,
            Overrides = [.. overrides]
        };

    private static readonly JsonSerializerOptions _WriteOptions = new(JsonCompat.SerializerOptions)
    {
        WriteIndented = true
    };
}
