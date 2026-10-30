using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static partial class GameDownload
{
    private static bool _dlPreferMojang;

    private static readonly string[] SourceArray = ["minecraftforge", "fabricmc", "neoforged"];

    /// <summary>
    ///     下载文件（而非获取版本列表）的时候，是否优先使用官方源。
    /// </summary>
    public static bool DlSourcePreferMojang =>
        Config.Download.FileSource == 2 || (Config.Download.FileSource == 1 && _dlPreferMojang);

    /// <summary>
    ///     获取版本列表（而非下载文件）的时候，是否优先使用官方源。
    /// </summary>
    public static bool DlVersionListPreferMojang =>
        Config.Download.VersionListSource == 2
        || (Config.Download.VersionListSource == 1 && _dlPreferMojang);

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理，返回字符串或 JSON 对象。
    ///     调用 NetGetCodeByRequest，会进行重试。
    /// </summary>
    public static T DlModRequest<T>(string url)
    {
        var urls = new List<KeyValuePair<string, int>>();
        var mcimUrl = DlSourceModGet(url);
        if (mcimUrl is not null && mcimUrl != url)
            switch (Config.Download.Comp.CompSourceSolution)
            {
                case 0:
                {
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 5));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                    urls.Add(new KeyValuePair<string, int>(url, 15));
                    break;
                }
                case 1:
                {
                    urls.Add(new KeyValuePair<string, int>(url, 5));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 5));
                    urls.Add(new KeyValuePair<string, int>(url, 15));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                    break;
                }

                default:
                {
                    urls.Add(new KeyValuePair<string, int>(url, 5));
                    urls.Add(new KeyValuePair<string, int>(url, 15));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                    break;
                }
            }

        var exs = "";
        foreach (var source in urls)
            try
            {
                var json = Requester.FetchString(
                    source.Key,
                    new RequestParam
                    {
                        Timeout = source.Value * 1000,
                        UseBrowserUserAgent = true
                    });
                return typeof(T) == typeof(string)
                    ? (T)(object)json
                    : (T)(object)Common.GetJson(json);
            }
            catch (Exception ex)
            {
                if (!ex.Message.ContainsF("mcimirror")) exs += $"{ex.Message}\r\n";
            }

        throw new Exception(exs);
    }

    /// <summary>
    ///     非泛型版本的 DlModRequest，返回 string
    ///     对可能涉及 Mod 镜像源的请求进行处理。
    ///     调用 NetRequest，会进行重试。
    /// </summary>
    public static string DlModRequest(
        string url,
        string method,
        string data,
        string contentType,
        bool allowMirror = false)
    {
        return DlModRequest<string>(url, method, data, contentType, allowMirror);
    }

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理。
    ///     调用 NetRequest，会进行重试。
    /// </summary>
    public static T DlModRequest<T>(
        string url,
        string method,
        string data,
        string contentType,
        bool allowMirror = false)
    {
        var urls = new List<KeyValuePair<string, int>>();
        var mcimUrl = DlSourceModGet(url);
        if (mcimUrl is not null && mcimUrl != url)
            switch (allowMirror ? Config.Download.Comp.CompSourceSolution : 2)
            {
                case 0:
                {
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 5));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                    urls.Add(new KeyValuePair<string, int>(url, 15));
                    break;
                }
                case 1:
                {
                    urls.Add(new KeyValuePair<string, int>(url, 5));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 5));
                    urls.Add(new KeyValuePair<string, int>(url, 15));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                    break;
                }

                default:
                {
                    urls.Add(new KeyValuePair<string, int>(url, 5));
                    urls.Add(new KeyValuePair<string, int>(url, 15));
                    urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                    break;
                }
            }

        var exs = "";
        foreach (var source in urls)
            try
            {
                var json = Requester.Fetch(
                    source.Key,
                    new FetchParam
                    {
                        Method = method,
                        Content = data,
                        ContentType = contentType,
                        Timeout = source.Value * 1000
                    });
                return typeof(T) == typeof(string)
                    ? (T)(object)json
                    : (T)(object)Common.GetJson(json);
            }
            catch (Exception ex)
            {
                if (!ex.Message.ContainsF("mcimirror")) exs += $"{ex.Message}\r\n";
            }

        throw new Exception(exs);
    }

    /// <summary>
    ///     下载文件（而非获取版本列表）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    /// </summary>
    public static IEnumerable<string> DlSourceOrder(
        IEnumerable<string> officialUrls,
        IEnumerable<string> mirrorUrls)
    {
        return DlSourcePreferMojang
            ? officialUrls.Union(mirrorUrls)
            : mirrorUrls.Union(officialUrls);
    }

    /// <summary>
    ///     获取版本列表（而非下载文件）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    /// </summary>
    public static IEnumerable<string> DlVersionListOrder(
        IEnumerable<string> officialUrls,
        IEnumerable<string> mirrorUrls)
    {
        return DlVersionListPreferMojang
            ? officialUrls.Union(mirrorUrls)
            : mirrorUrls.Union(officialUrls);
    }

    /// <summary>
    ///     下载 Assets 文件。
    /// </summary>
    public static IEnumerable<string> DlSourceAssetsGet(string original)
    {
        original = original
            .Replace(
                "http://resources.download.minecraft.net",
                "https://resources.download.minecraft.net");
        return DlSourceOrder(
            [original],
            [
                original
                    .Replace(
                        "https://piston-data.mojang.com",
                        "https://bmclapi2.bangbang93.com/assets")
                    .Replace(
                        "https://piston-meta.mojang.com",
                        "https://bmclapi2.bangbang93.com/assets")
                    .Replace(
                        "https://resources.download.minecraft.net",
                        "https://bmclapi2.bangbang93.com/assets")
            ]);
    }

    /// <summary>
    ///     下载 Libraries 文件。
    /// </summary>
    public static IEnumerable<string> DlSourceLibraryGet(string original)
    {
        if (SourceArray.Any(original.Contains))
            return
            [
                original
                    .Replace(
                        "https://piston-data.mojang.com",
                        "https://bmclapi2.bangbang93.com/maven")
                    .Replace(
                        "https://piston-meta.mojang.com",
                        "https://bmclapi2.bangbang93.com/maven")
                    .Replace(
                        "https://libraries.minecraft.net",
                        "https://bmclapi2.bangbang93.com/maven")
                    .Replace(
                        "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                original
                    .Replace(
                        "https://piston-data.mojang.com",
                        "https://bmclapi2.bangbang93.com/libraries")
                    .Replace(
                        "https://piston-meta.mojang.com",
                        "https://bmclapi2.bangbang93.com/libraries")
                    .Replace(
                        "https://libraries.minecraft.net",
                        "https://bmclapi2.bangbang93.com/libraries")
                    .Replace(
                        "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto")
            ];

        return DlSourceOrder(
            [original],
            [
                original
                    .Replace(
                        "https://piston-data.mojang.com",
                        "https://bmclapi2.bangbang93.com/maven")
                    .Replace(
                        "https://piston-meta.mojang.com",
                        "https://bmclapi2.bangbang93.com/maven")
                    .Replace(
                        "https://libraries.minecraft.net",
                        "https://bmclapi2.bangbang93.com/maven")
                    .Replace(
                        "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                original
                    .Replace(
                        "https://piston-data.mojang.com",
                        "https://bmclapi2.bangbang93.com/libraries")
                    .Replace(
                        "https://piston-meta.mojang.com",
                        "https://bmclapi2.bangbang93.com/libraries")
                    .Replace(
                        "https://libraries.minecraft.net",
                        "https://bmclapi2.bangbang93.com/libraries")
                    .Replace(
                        "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                original
            ]);
    }

    /// <summary>
    ///     下载 Launcher 或 Meta 文件。
    ///     不应使用它来获取版本列表（因为它只使用文件下载源设置来决定源顺序）。
    /// </summary>
    public static IEnumerable<string> DlSourceLauncherOrMetaGet(string original)
    {
        if (original is null)
            throw new Exception(Lang.Text("Minecraft.Download.Error.NoJsonDownloadAddress"));
        return DlSourceOrder(
            [original],
            [
                original
                    .Replace(
                        "https://piston-data.mojang.com",
                        "https://bmclapi2.bangbang93.com")
                    .Replace(
                        "https://piston-meta.mojang.com",
                        "https://bmclapi2.bangbang93.com")
                    .Replace(
                        "https://launcher.mojang.com",
                        "https://bmclapi2.bangbang93.com")
                    .Replace(
                        "https://launchermeta.mojang.com",
                        "https://bmclapi2.bangbang93.com")
                    .Replace(
                        "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                        "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto"),
                original
            ]);
    }

    /// <summary>
    ///     Mod Api 镜像源
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
    public static string DlSourceModGet(string original)
    {
        return original
            .Replace(
                "https://api.modrinth.com",
                "https://mod.mcimirror.top/modrinth")
            .Replace(
                "https://api.curseforge.com",
                "https://mod.mcimirror.top/curseforge");
    }

    /// <summary>
    ///     Mod 下载镜像源
    /// </summary>
    /// <param name="original"></param>
    /// <returns></returns>
    public static List<string> DlSourceModDownloadGet(string original)
    {
        var res = new List<string>();
        var mirrorDl = original
            .Replace(
                "https://cdn.modrinth.com",
                "https://mod.mcimirror.top")
            .Replace(
                "https://edge.forgecdn.net",
                "https://mod.mcimirror.top");
        switch (Config.Download.Comp.CompSourceSolution)
        {
            case 0:
            {
                res.Add(mirrorDl);
                res.Add(mirrorDl);
                break;
            }
            case 1:
            {
                res.Add(original);
                res.Add(mirrorDl);
                break;
            }
            case 2:
            {
                res.Add(original);
                res.Add(original);
                break;
            }

            default:
            {
                Config.Download.Comp.CompSourceSolution = 1;
                res.Add(original);
                break;
            }
        }

        res.Add(original);
        return res;
    }

    private static void DlSourceLoader<InputType, OutputType>(
        Loading.LoaderTask<InputType, OutputType> mainLoader,
        List<KeyValuePair<Loading.LoaderTask<InputType, OutputType>, int>> loaderList,
        bool isForceRestart = false)
    {
        var waitCycle = 0;
        while (true)
        {
            var beforeLoadersAllFailed = true;
            foreach (var subLoader in loaderList)
            {
                if (waitCycle == 0)
                {
                    if (isForceRestart)
                        continue;
                    if (subLoader.Key.input is null ^ mainLoader.input is null
                        || (subLoader.Key.input is not null
                            && !subLoader.Key.input.Equals(mainLoader.input)))
                        continue;
                }

                if (subLoader.Key.State != Common.LoadState.Failed)
                    beforeLoadersAllFailed = false;
                if (subLoader.Key.State == Common.LoadState.Finished)
                {
                    mainLoader.output = subLoader.Key.output;
                    DlSourceLoaderAbort(loaderList);
                    return;
                }

                if (!beforeLoadersAllFailed) continue;
                if (waitCycle < subLoader.Value * 100)
                    waitCycle = subLoader.Value * 100;
            }

            if (waitCycle == 0)
            {
                loaderList.First().Key.Start(mainLoader.input, isForceRestart);
                foreach (var loader in loaderList.Skip(1))
                    loader.Key.State = Common.LoadState.Waiting;
            }

            for (int i = 0, loopTo = loaderList.Count - 1; i <= loopTo; i++)
            {
                if (waitCycle != loaderList[i].Value * 100)
                    continue;
                if (i < loaderList.Count - 1
                    && loaderList.Any(l => l.Key.State != Common.LoadState.Failed))
                {
                    loaderList[i + 1].Key.Start(mainLoader.input, isForceRestart);
                }
                else
                {
                    Exception? errorInfo = null;
                    for (int ii = 0, loopTo1 = loaderList.Count - 1; ii <= loopTo1; ii++)
                    {
                        loaderList[ii].Key.input = default;
                        if (loaderList[ii].Key.Error is null) continue;
                        if (errorInfo is null
                            || loaderList[ii]
                                .Key.Error.Message
                                .Contains(Lang.Text("Minecraft.Download.Error.NotFound")))
                            errorInfo = loaderList[ii].Key.Error;
                    }

                    errorInfo ??=
                        new TimeoutException(Lang.Text("Minecraft.Download.Error.Timeout"));
                    DlSourceLoaderAbort(loaderList);
                    throw errorInfo;
                }

                break;
            }

            Thread.Sleep(10);
            waitCycle += 1;
            if (!mainLoader.IsAborted) continue;
            DlSourceLoaderAbort(loaderList);
            return;
        }
    }

    private static void DlSourceLoaderAbort<InputType, OutputType>(
        List<KeyValuePair<Loading.LoaderTask<InputType, OutputType>, int>> loaderList)
    {
        foreach (var loader in loaderList.Where(loader =>
                     loader.Key.State == Common.LoadState.Loading))
            loader.Key.Abort();
    }
}
