using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using fNbt;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Core.Minecraft.Saves.Editing.Internal;
using PCL.Core.Minecraft.Saves.Exceptions;
using PCL.Core.Minecraft.Saves.Parsing;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档管理器 —— 存档系统的统一入口。
/// 提供扫描、读取、批量读取和修改存档的功能。
/// 可通过构造函数注入自定义的解析器工厂和编辑器列表。
/// </summary>
public class SaveManager
{
    private readonly SaveParserFactory _parserFactory;
    private readonly IReadOnlyList<ISaveEditor> _editors;

    /// <summary>
    /// 创建新的存档管理器。
    /// </summary>
    /// <param name="parserFactory">自定义解析器工厂，为 null 时使用默认工厂。</param>
    /// <param name="customEditors">自定义编辑器列表，为 null 时使用默认编辑器。</param>
    public SaveManager(
        SaveParserFactory? parserFactory = null,
        IEnumerable<ISaveEditor>? customEditors = null)
    {
        _parserFactory = parserFactory ?? new SaveParserFactory();
        _editors = customEditors?.ToArray() ?? [new Pre261SaveEditor(), new Version261PlusSaveEditor()];
    }

    /// <summary>
    /// 扫描指定目录下的所有有效存档文件夹，返回按最后游玩时间降序排列的列表。
    /// 不含 level.dat 的文件夹会被静默跳过。
    /// </summary>
    /// <param name="savesPath">存档根目录（通常为 .minecraft/saves）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task<IReadOnlyList<SaveInfo>> ScanSaveFoldersAsync(
        string savesPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(savesPath))
            return [];

        var folderPaths = Directory.GetDirectories(savesPath);
        var results = new List<SaveInfo>(folderPaths.Length);

        foreach (var folder in folderPaths)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = await LoadSaveAsync(folder, ct).ConfigureAwait(false);
                if (info is not null)
                    results.Add(info);
            }
            catch (SaveNotFoundException)
            {
                // 非存档文件夹，静默跳过
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Saves", $"扫描存档文件夹失败：{folder}");
            }
        }

        return results.OrderByDescending(s => s.LastPlayedUtc).ToList();
    }

    /// <summary>
    /// 加载指定文件夹中的单个存档。
    /// </summary>
    /// <param name="folderPath">存档文件夹的绝对路径。</param>
    /// <param name="ct">取消令牌。</param>
    /// <exception cref="SaveNotFoundException">level.dat 缺失。</exception>
    /// <exception cref="SaveCorruptedException">level.dat 存在但无法解析。</exception>
    public Task<SaveInfo> LoadSaveAsync(string folderPath, CancellationToken ct = default)
    {
        var levelDatPath = ResolveLevelDatPath(folderPath);
        if (levelDatPath is null)
            throw new SaveNotFoundException(folderPath);

        return LoadFromPathAsync(folderPath, levelDatPath, ct);
    }

    /// <summary>
    /// 批量异步加载存档目录下的所有存档，每解析完一个即通过 IAsyncEnumerable 向外产出。
    /// 无法加载的存档会被记录日志并跳过，不会中断整个枚举。
    /// </summary>
    /// <param name="savesPath">存档根目录。</param>
    /// <param name="ct">取消令牌。</param>
    public async IAsyncEnumerable<SaveInfo> LoadSavesAsync(
        string savesPath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!Directory.Exists(savesPath))
            yield break;

        var folderPaths = Directory.GetDirectories(savesPath);
        foreach (var folder in folderPaths)
        {
            ct.ThrowIfCancellationRequested();

            SaveInfo? info = null;
            try
            {
                info = await LoadSaveAsync(folder, ct).ConfigureAwait(false);
            }
            catch (SaveNotFoundException)
            {
                // 非存档文件夹，跳过
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Saves", $"加载存档失败：{folder}");
            }

            if (info is not null)
                yield return info;
        }
    }

    /// <summary>
    /// 将指定的修改应用到某个存档。
    /// </summary>
    /// <param name="folderPath">存档文件夹的绝对路径。</param>
    /// <param name="changes">要应用的修改集合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>至少有一项修改成功写入时返回 true。</returns>
    /// <exception cref="SaveNotFoundException">level.dat 缺失。</exception>
    /// <exception cref="SaveCorruptedException">level.dat 解析或写入失败。</exception>
    public async Task<bool> ApplyChangesAsync(
        string folderPath, SaveChanges changes, CancellationToken ct = default)
    {
        // 无修改时直接返回，避免不必要的文件 IO
        if (changes.IsEmpty)
            return false;

        var levelDatPath = ResolveLevelDatPath(folderPath)
            ?? throw new SaveNotFoundException(folderPath);

        // 一次解析 level.dat，提取 Data 复合标签和 DataVersion
        NbtFile nbtFile;
        NbtCompound data;
        try
        {
            nbtFile = new NbtFile();
            await Task.Run(() =>
            {
                using var fs = new FileStream(levelDatPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                nbtFile.LoadFromStream(fs, NbtCompression.AutoDetect);
            }, ct).ConfigureAwait(false);

            data = nbtFile.RootTag.Get<NbtCompound>("Data")
                ?? throw new InvalidDataException("level.dat 中缺少 Data 复合标签");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SaveCorruptedException(folderPath, $"解析 level.dat 失败：'{levelDatPath}'", ex);
        }

        var dataVersion = ReadDataVersionFromCompound(data);

        // 匹配编辑器，执行内存修改
        foreach (var editor in _editors)
        {
            if (editor.CanHandle(dataVersion))
            {
                try
                {
                    if (!editor.ApplyChanges(data, changes))
                        return false;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new SaveCorruptedException(folderPath,
                        $"应用修改失败：'{folderPath}'", ex);
                }

                // 原子写入：temp → 备份 → 重命名
                await WriteLevelDatAtomicallyAsync(levelDatPath, nbtFile, ct).ConfigureAwait(false);
                return true;
            }
        }

        // 找不到匹配编辑器时抛出异常，与"无修改"（返回 false）区分
        throw new SaveCorruptedException(folderPath,
            $"找不到匹配的存档编辑器（DataVersion: {dataVersion}）");
    }

    /// <summary>
    /// 原子写入 level.dat：先将 NBT 写入临时文件，再通过重命名完成原子替换。
    /// 始终写入 level.dat（即使从 level.dat_old 回退读取）。
    /// </summary>
    private static async Task WriteLevelDatAtomicallyAsync(
        string sourcePath, NbtFile nbtFile, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(sourcePath)!;
        var tempPath = Path.Combine(dir, $"level{Guid.NewGuid():N}.dat");
        var backupPath = Path.Combine(dir, "level.dat_old");
        var targetPath = Path.Combine(dir, "level.dat");

        try
        {
            // 1. 写入临时文件
            await Task.Run(() =>
            {
                using var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, true);
                nbtFile.SaveToStream(fs, NbtCompression.GZip);
            }, ct).ConfigureAwait(false);

            // 2. 仅当从 level.dat 读取时，才备份当前 level.dat → level.dat_old；
            //    若从 level.dat_old 回退读取，说明 level.dat 已损坏/不存在，跳过备份。
            if (sourcePath == targetPath && File.Exists(targetPath))
            {
                File.Move(targetPath, backupPath, overwrite: true);
            }

            // 3. 重命名临时文件 → level.dat
            File.Move(tempPath, targetPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }

    /// <summary>核心加载逻辑：读取 level.dat → 解析 DataVersion → 匹配解析器 → 构建 SaveInfo。</summary>
    private async Task<SaveInfo> LoadFromPathAsync(
        string folderPath, string levelDatPath, CancellationToken ct)
    {
        NbtFile nbtFile;
        try
        {
            nbtFile = await LoadNbtFileAsync(levelDatPath, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SaveCorruptedException(folderPath,
                $"解析 level.dat 失败：'{levelDatPath}'", ex);
        }

        // level.dat 根标签下必须有 Data 复合标签
        var data = nbtFile.RootTag.Get<NbtCompound>("Data")
            ?? throw new SaveCorruptedException(folderPath,
                $"level.dat 中缺少 Data 复合标签：{levelDatPath}");

        var dataVersion = ReadDataVersionFromCompound(data);
        var createdAt = Directory.GetCreationTimeUtc(folderPath);
        var modifiedAt = File.GetLastWriteTimeUtc(levelDatPath);

        var parser = _parserFactory.Resolve(data, dataVersion)
            ?? throw new SaveCorruptedException(folderPath,
                $"找不到与存档 '{folderPath}' 匹配的解析器（DataVersion: {dataVersion}）");

        return parser.Parse(folderPath, data, createdAt, modifiedAt);
    }

    /// <summary>以异步方式加载 NBT 文件，自动检测压缩格式。</summary>
    private static async Task<NbtFile> LoadNbtFileAsync(string path, CancellationToken ct)
    {
        var nbtFile = new NbtFile();
        await Task.Run(() =>
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            nbtFile.LoadFromStream(fs, NbtCompression.AutoDetect);
        }, ct).ConfigureAwait(false);
        return nbtFile;
    }

    /// <summary>
    /// 确定 level.dat 的路径。
    /// 优先查找 level.dat，如果不存在则查找 level.dat_old 作为备份。
    /// </summary>
    private static string? ResolveLevelDatPath(string folderPath)
    {
        var primary = Path.Combine(folderPath, "level.dat");
        if (File.Exists(primary))
            return primary;
        var backup = Path.Combine(folderPath, "level.dat_old");
        return File.Exists(backup) ? backup : null;
    }

    /// <summary>从 Data 复合标签中读取 DataVersion 字段。</summary>
    private static int? ReadDataVersionFromCompound(NbtCompound data)
    {
        if (data.TryGet<NbtInt>("DataVersion", out var dv))
            return dv!.Value;
        return null;
    }

}
