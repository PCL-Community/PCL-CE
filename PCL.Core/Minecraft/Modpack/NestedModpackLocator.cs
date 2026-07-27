using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 在压缩包内查找被再次打包的整合包。
/// <para>
/// 相当一部分整合包在分发时会被再套一层压缩包，典型情形是「附带启动器的整合包」——
/// 外层压缩包里放着启动器与配套文件，真正的整合包是其中的 <c>modpack.zip</c> 或
/// <c>modpack.mrpack</c>。本类负责把内层整合包取出来，使其能走正常的安装流程。
/// </para>
/// <para>
/// 外层压缩包里的其他内容（启动器可执行文件、说明文档等）一律忽略。
/// </para>
/// </summary>
public static class NestedModpackLocator
{
    /// <summary>约定的内层整合包文件名，优先尝试。</summary>
    private static readonly string[] _ConventionalNames = ["modpack.mrpack", "modpack.zip"];

    /// <summary>可能是整合包的扩展名。</summary>
    private static readonly string[] _ArchiveExtensions = [".mrpack", ".zip"];

    /// <summary>
    /// 最多尝试的候选数量。逐个候选都要完整解压一次，
    /// 因此对含有大量压缩包的外层包设上限，避免长时间无谓的磁盘读写。
    /// </summary>
    public const int MaxCandidates = 8;

    /// <summary>
    /// 存放取出的内层整合包的目录。
    /// </summary>
    public static string TemporaryDirectory { get; } = _ResolveTemporaryDirectory();

    /// <summary>
    /// 解析临时目录。优先使用启动器的临时目录；当本程序集被作为库单独引用
    /// （例如单元测试宿主）时 <see cref="Paths"/> 无法初始化，此时退回系统临时目录。
    /// </summary>
    private static string _ResolveTemporaryDirectory()
    {
        try
        {
            return Path.Combine(Paths.Temp, "Modpack");
        }
        catch (Exception)
        {
            return Path.Combine(Path.GetTempPath(), "PCLCE", "Modpack");
        }
    }

    /// <summary>
    /// 尝试从压缩包中取出一个可识别的内层整合包。
    /// </summary>
    /// <param name="archive">外层压缩包。</param>
    /// <param name="identifier">用于判定候选是否为已知整合包格式。</param>
    /// <returns>
    /// 解压出的临时文件路径；未找到时返回 <c>null</c>。
    /// 返回的文件由调用方负责删除。
    /// </returns>
    public static async Task<string?> TryExtractAsync(
        ModpackArchive archive, ModpackIdentifier identifier, CancellationToken cancellationToken = default)
    {
        foreach (var candidate in _RankCandidates(archive).Take(MaxCandidates))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tempPath = await _TryExtractToTempAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (tempPath is null) continue;

            if (_IsRecognizedModpack(tempPath, identifier))
            {
                LogWrapper.Info("Modpack", $"已从外层压缩包中取出内层整合包：{candidate.RelativePath}");
                return tempPath;
            }

            DeleteTemporaryFile(tempPath);
        }

        return null;
    }

    /// <summary>
    /// 按可能性排序候选条目：约定文件名最优先，其次是 <c>.mrpack</c>，最后是其他 <c>.zip</c>。
    /// </summary>
    private static IEnumerable<ModpackArchiveItem> _RankCandidates(ModpackArchive archive)
        => archive.EnumerateFiles()
            .Where(item => item.Entry.Length > 0)
            .Select(item => (Item: item, Rank: _GetRank(item.RelativePath)))
            .Where(pair => pair.Rank >= 0)
            .OrderBy(pair => pair.Rank)
            // 同级别下取路径较浅者，通常更接近外层包的主要内容；再以路径排序保证结果稳定
            .ThenBy(pair => pair.Item.RelativePath.Count(c => c == '/'))
            .ThenBy(pair => pair.Item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Item);

    /// <returns>候选优先级，数值越小越优先；不是候选时返回 -1。</returns>
    private static int _GetRank(string relativePath)
    {
        var fileName = relativePath.Split('/')[^1];

        if (_ConventionalNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)) return 0;
        if (fileName.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)) return 1;
        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return 2;

        return -1;
    }

    private static async Task<string?> _TryExtractToTempAsync(
        ModpackArchiveItem item, CancellationToken cancellationToken)
    {
        var extension = _ArchiveExtensions.FirstOrDefault(
            ext => item.RelativePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) ?? ".zip";

        Directory.CreateDirectory(TemporaryDirectory);
        var tempPath = Path.Combine(TemporaryDirectory, $"nested-{Guid.NewGuid():N}{extension}");

        try
        {
            await using var source = item.Entry.Open();
            await using var destination = File.Create(tempPath);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return tempPath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeleteTemporaryFile(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Modpack", $"解压内层整合包候选失败（{item.RelativePath}）：{ex.Message}");
            DeleteTemporaryFile(tempPath);
            return null;
        }
    }

    private static bool _IsRecognizedModpack(string filePath, ModpackIdentifier identifier)
    {
        try
        {
            using var archive = ModpackArchive.Open(filePath);
            return identifier.Identify(archive) is not null;
        }
        catch (ModpackArchiveException)
        {
            return false;
        }
    }

    /// <summary>删除解压产生的临时文件，失败不影响流程。</summary>
    public static void DeleteTemporaryFile(string? path)
    {
        if (path is null || !File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogWrapper.Debug("Modpack", $"删除临时文件失败：{path}");
        }
    }
}
