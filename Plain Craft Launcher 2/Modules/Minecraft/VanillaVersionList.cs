using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

internal static class VanillaVersionList
{
    private static Task? _refreshTask;
    private static readonly object RefreshGate = new();

    private const string UvmcUrl =
        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto/version_manifest.json";

    private const int RefreshRetries = 3;

    [ModuleInitializer]
    internal static void Register()
    {
        VanillaVersionIndex.SetEmbeddedReader(ReadLocal);
        VanillaVersionIndex.BeginRefresh = () => { _ = RefreshAsync(); };
    }

    private static IReadOnlyList<string> ReadEmbedded()
    {
        var uri = new Uri("pack://application:,,,/Plain Craft Launcher 2;component/Resources/versions.txt", UriKind.Absolute);
        using var stream = Application.GetResourceStream(uri)!.Stream;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return VanillaVersionCache.Parse(reader.ReadToEnd());
    }

    private static string CachePath => Path.Combine(Paths.SharedLocalData, "versions.txt");

    private static IReadOnlyList<string> ReadLocal()
    {
        var embedded = ReadEmbedded();
        var cached = VanillaVersionCache.TryRead(CachePath);
        if (cached is not null && cached.Count > embedded.Count)
        {
            ModBase.Log($"[Minecraft] 使用缓存的 versions 列表，共 {cached.Count} 个版本");
            return cached;
        }
        return embedded;
    }

    public static Task RefreshAsync()
    {
        lock (RefreshGate)
        {
            if (_refreshTask is { IsCompleted: false })
                return _refreshTask;
            _refreshTask = Task.Run(RefreshCore);
            return _refreshTask;
        }
    }

    private static void RefreshCore()
    {
        ModBase.Log("[Minecraft] 开始获取 versions 列表");
        Exception? last = null;
        for (var attempt = 0; attempt <= RefreshRetries; attempt++)
        {
            try
            {
                var ids = DownloadIds();
                VanillaVersionIndex.Publish(ids);
                try
                {
                    VanillaVersionCache.Write(CachePath, ids);
                    ModBase.Log($"[Minecraft] versions 列表获取完成，共 {ids.Count} 个版本，已更新缓存");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ModBase.Log(ex, "[Minecraft] versions 列表获取完成，但写入缓存失败，已保留原缓存",
                        ModBase.LogLevel.Normal);
                }

                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
            {
                last = ex;
                if (attempt >= RefreshRetries)
                    break;
                ModBase.Log(ex, $"[Minecraft] 获取 versions 列表失败，正在重试（{attempt + 1}/{RefreshRetries}）",
                    ModBase.LogLevel.Normal);
            }
        }

        ModBase.Log(last!, "[Minecraft] 获取 versions 列表失败", ModBase.LogLevel.Normal);
    }

    private static List<string> DownloadIds()
    {
        var primary = Config.Download.VersionListSource == 0
            ? "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"
            : "https://launchermeta.mojang.com/mc/game/version_manifest.json";
        var merged = ReadManifest(primary);
        foreach (var pair in ReadManifest(UvmcUrl))
            merged[pair.Key] = pair.Value;
        return merged
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .ToList();
    }

    private static Dictionary<string, DateTime> ReadManifest(string url)
    {
        var json = (JsonObject)Requester.FetchJson(url);
        var versions = (JsonArray)json["versions"]!;
        var map = new Dictionary<string, DateTime>(versions.Count, StringComparer.Ordinal);
        foreach (JsonObject entry in versions)
        {
            var id = (string)entry["id"]!;
            if (!JsonCompat.TryGetDateTime(entry["releaseTime"], out var time))
                continue;
            map[id] = time;
        }

        return map;
    }
}
