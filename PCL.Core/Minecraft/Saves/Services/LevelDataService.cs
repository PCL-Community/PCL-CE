using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fNbt;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Saves.Models;
using PCL.Core.Minecraft.Saves.Parsers;
using PCL.Core.Minecraft.Saves.Writers;

namespace PCL.Core.Minecraft.Saves.Services;

/// <summary>
/// 存档数据服务（统一入口）
/// </summary>
public class LevelDataService
{
    /// <summary>
    /// 异步加载存档信息
    /// </summary>
    /// <param name="levelDatPath">level.dat 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含 LevelDataInfo 和内部 NbtFile 的包装对象，可用于后续写入</returns>
    public async Task<LevelDataLoadResult?> LoadAsync(string levelDatPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // 直接读取整个文件
            var nbtFile = new NbtFile();
            await Task.Run(() =>
            {
                using var fs = new FileStream(levelDatPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                nbtFile.LoadFromStream(fs, NbtCompression.AutoDetect);
            }, cancellationToken);

            var dataTag = nbtFile.RootTag?.Get<NbtCompound>("Data");
            if (dataTag == null)
            {
                LogWrapper.Warn($"无法读取 level.dat 中的 Data 标签: {levelDatPath}");
                return null;
            }

            // 选择解析器
            var isModern = VersionDetector.IsModernFormat(dataTag);
            ILevelDataParser parser = isModern ? new ModernLevelDataParser() : new LegacyLevelDataParser();

            // 解析
            var saveFolderPath = Path.GetDirectoryName(levelDatPath) ?? string.Empty;
            var info = parser.Parse(dataTag, saveFolderPath);

            return new LevelDataLoadResult
            {
                Info = info,
                NbtFile = nbtFile,
                IsModern = isModern
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"解析存档数据失败: {levelDatPath}");
            return null;
        }
    }

    /// <summary>
    /// 保存存档数据
    /// </summary>
    public async Task<bool> SaveAsync(string levelDatPath, NbtFile nbtFile, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(levelDatPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await Task.Run(() =>
            {
                using var fs = new FileStream(levelDatPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                nbtFile.SaveToStream(fs, NbtCompression.GZip);
            }, cancellationToken);

            LogWrapper.Info($"NBT 文件成功保存于：{levelDatPath}");
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"保存 level.dat 失败: {levelDatPath}");
            return false;
        }
    }

    /// <summary>
    /// 根据已加载的 NbtFile 获取写入器
    /// </summary>
    public ILevelDataWriter GetWriter(NbtFile nbtFile)
    {
        var dataTag = nbtFile.RootTag?.Get<NbtCompound>("Data");
        if (dataTag == null)
            throw new InvalidOperationException("Invalid level.dat: missing Data tag");

        var isModern = VersionDetector.IsModernFormat(dataTag);
        return isModern ? new ModernLevelDataWriter() : new LegacyLevelDataWriter();
    }
}

/// <summary>
/// 加载结果包装类
/// </summary>
public class LevelDataLoadResult
{
    public LevelDataInfo Info { get; set; } = null!;
    public NbtFile NbtFile { get; set; } = null!;
    public bool IsModern { get; set; }
}