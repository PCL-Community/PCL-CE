using System.Text;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

/// <summary>
///     下载纯 Json 型加载器（Fabric / Legacy Fabric）的版本描述文件。
///     这类加载器不需要运行外部安装器，版本 Json 与支持库列表即可完成安装。
/// </summary>
internal static class LoaderProfileInstall
{
    public static Loading.LoaderTask<string, string> CreateProfileLoader(
        McInstallRequest request,
        string tempFolder,
        bool legacy)
    {
        var minecraftName = request.minecraftName
            ?? throw new Exception(
                Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var loaderVersion = legacy
            ? request.legacyFabricVersion
            ?? throw new InvalidOperationException("Legacy Fabric 版本为空")
            : request.fabricVersion
            ?? throw new InvalidOperationException("Fabric 版本为空");
        var loaderId = legacy
            ? $"legacy-fabric-loader-{loaderVersion}-{minecraftName}"
            : $"fabric-loader-{loaderVersion}-{minecraftName}";
        var versionFolder = Path.Combine(tempFolder, "versions", loaderId);

        return new Loading.LoaderTask<string, string>(
            Lang.Text(
                legacy
                    ? "Minecraft.Download.Stage.ObtainLegacyFabricMainFileUrl"
                    : "Minecraft.Download.Stage.ObtainFabricMainFileUrl"),
            task =>
            {
                var safeMinecraftName = minecraftName.Replace("∞", "infinite");
                string[] urls = legacy
                    ?
                    [
                        "https://meta.legacyfabric.net/v2/versions/loader/"
                        + $"{safeMinecraftName}/{loaderVersion}/profile/json"
                    ]
                    :
                    [
                        "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/"
                        + $"{safeMinecraftName}/{loaderVersion}/profile/json",
                        "https://meta.fabricmc.net/v2/versions/loader/"
                        + $"{safeMinecraftName}/{loaderVersion}/profile/json"
                    ];

                string? json = null;
                var errors = new List<Exception>();
                foreach (var url in urls)
                    try
                    {
                        var text = Requester.FetchString(
                            url,
                            new RequestParam
                            {
                                UseBrowserUserAgent = true,
                                Timeout = 5000,
                                Retries = 2
                            });
                        if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith('{'))
                        {
                            json = text;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                        LauncherCommon.Log(ex, $"[Install] 从 {url} 获取加载器版本 Json 失败");
                    }

                if (json is null)
                    throw new Exception(
                        Lang.Text(
                            "Minecraft.Download.Error.FabricMetaDownloadFailed",
                            string.Join(" and ", urls)),
                        errors.FirstOrDefault());

                Directory.CreateDirectory(versionFolder);
                var jsonPath = Path.Combine(versionFolder, $"{loaderId}.json");
                LauncherCommon.WriteFile(jsonPath, json, new UTF8Encoding(false));
                task.output = jsonPath;
                LauncherCommon.Log($"[Install] 已获取 {loaderId} 版本 Json：{jsonPath}");
            })
        {
            ProgressWeight = 2d
        };
    }
}