using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Modpack.CurseForge;
using PCL.Core.Utils;

namespace PCL.Modpack;

/// <summary>
///     基于 PCL 网络层的 <see cref="ICurseForgeFileResolver" /> 实现。
///     负责镜像源选择、API 密钥签名与响应解析，不含任何格式判断逻辑。
/// </summary>
public sealed class PclCurseForgeFileResolver : ICurseForgeFileResolver
{
    /// <summary>共享实例。</summary>
    public static PclCurseForgeFileResolver Instance { get; } = new();

    /// <summary>单次请求的最大文件数，超出时分批。</summary>
    private const int BatchSize = 200;

    public async Task<IReadOnlyList<CurseForgeFileDescriptor>> ResolveAsync(
        IReadOnlyList<CurseForgeFileKey> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0) return [];

        // 文件信息与项目分类分属两个端点：前者给出文件名与地址，后者给出 classId
        var files = await Task.Run(() => _RequestFiles(keys, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        var classIds = await Task.Run(() => _RequestClassIds(keys, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        var result = new List<CurseForgeFileDescriptor>(files.Count);
        foreach (var file in files)
        {
            result.Add(file with
            {
                ClassId = classIds.TryGetValue(file.ProjectId, out var classId) ? classId : null
            });
        }

        return result;
    }

    /// <summary>
    ///     调用 <c>POST /v1/mods/files</c> 批量取得文件信息。
    /// </summary>
    private static List<CurseForgeFileDescriptor> _RequestFiles(
        IReadOnlyList<CurseForgeFileKey> keys, CancellationToken cancellationToken)
    {
        var descriptors = new List<CurseForgeFileDescriptor>(keys.Count);

        foreach (var batch in _Batch(keys, BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileIds = string.Join(",", batch.Select(key => key.FileId));
            var payload = $"{{\"fileIds\": [{fileIds}]}}";

            // 镜像源偶尔返回不完整的结果，此时退回官方源重试一次
            var data = _RequestWithMirrorFallback(payload, batch.Count, cancellationToken);
            if (data is null) continue;

            foreach (var node in data)
            {
                if (node is not JsonObject file) continue;
                if (_ParseFile(file) is { } descriptor) descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    private static JsonArray? _RequestWithMirrorFallback(
        string payload, int expectedCount, CancellationToken cancellationToken)
    {
        foreach (var allowMirror in new[] { true, false })
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = ModDownload.DlModRequest<JsonObject>(
                    "https://api.curseforge.com/v1/mods/files",
                    "POST", payload, "application/json", allowMirror);

                if (response?["data"] is not JsonArray data) continue;

                // 结果完整，或已是官方源的结果，都直接采用
                if (data.Count >= expectedCount || !allowMirror) return data;

                ModBase.Log($"[Modpack] 镜像源返回的文件信息不完整（{data.Count}/{expectedCount}），改用官方源");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Modpack] 获取 CurseForge 文件信息失败（镜像源：{allowMirror}）");
                if (!allowMirror) throw;
            }
        }

        return null;
    }

    /// <summary>
    ///     调用 <c>POST /v1/mods</c> 取得各项目的分类 ID，用于决定文件的落地目录。
    /// </summary>
    private static Dictionary<int, int> _RequestClassIds(
        IReadOnlyList<CurseForgeFileKey> keys, CancellationToken cancellationToken)
    {
        var classIds = new Dictionary<int, int>();
        var projectIds = keys.Select(key => key.ProjectId).Distinct().ToList();

        foreach (var batch in _Batch(projectIds, BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var payload = $"{{\"modIds\": [{string.Join(",", batch)}]}}";
                var response = ModDownload.DlModRequest<JsonObject>(
                    "https://api.curseforge.com/v1/mods",
                    "POST", payload, "application/json", true);

                if (response?["data"] is not JsonArray data) continue;

                foreach (var node in data)
                {
                    if (node is not JsonObject project) continue;
                    if (project["id"]?.ToObject<int?>() is not { } id) continue;
                    if (project["classId"]?.ToObject<int?>() is { } classId) classIds[id] = classId;
                }
            }
            catch (Exception ex)
            {
                // 分类信息缺失只影响目录推断，不影响下载，因此不向上抛出
                ModBase.Log(ex, "[Modpack] 获取 CurseForge 项目分类失败，将按文件内容推断资源种类");
            }
        }

        return classIds;
    }

    private static CurseForgeFileDescriptor? _ParseFile(JsonObject file)
    {
        var fileId = file["id"]?.ToObject<int?>();
        var projectId = file["modId"]?.ToObject<int?>();
        var fileName = file["fileName"]?.ToString();

        if (fileId is not { } id || projectId is not { } project || string.IsNullOrWhiteSpace(fileName))
            return null;

        var rawUrl = file["downloadUrl"]?.ToString();
        var downloadUrl = string.IsNullOrWhiteSpace(rawUrl)
            ? null
            : ModComp.CompFile.HandleCurseForgeDownloadUrls(rawUrl);

        return new CurseForgeFileDescriptor
        {
            ProjectId = project,
            FileId = id,
            FileName = fileName,
            DownloadUrl = downloadUrl,
            DisplayName = file["displayName"]?.ToString(),
            Sha1 = _ParseSha1(file["hashes"] as JsonArray),
            FileSize = file["fileLength"]?.ToObject<long?>(),
            ModuleNames = _ParseModuleNames(file["modules"] as JsonArray)
        };
    }

    /// <summary>
    ///     CurseForge 的 <c>hashes</c> 为 <c>[{value, algo}]</c>，其中 algo 为 1 表示 SHA-1。
    /// </summary>
    private static string? _ParseSha1(JsonArray? hashes)
    {
        foreach (var node in hashes ?? [])
        {
            if (node is not JsonObject hash) continue;
            if (hash["algo"]?.ToObject<int?>() != 1) continue;

            var value = hash["value"]?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim().ToLowerInvariant();
        }

        return null;
    }

    private static List<string> _ParseModuleNames(JsonArray? modules)
    {
        var names = new List<string>();

        foreach (var node in modules ?? [])
        {
            var name = (node as JsonObject)?["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }

        return names;
    }

    private static IEnumerable<List<T>> _Batch<T>(IReadOnlyList<T> source, int size)
    {
        for (var offset = 0; offset < source.Count; offset += size)
            yield return source.Skip(offset).Take(size).ToList();
    }
}
