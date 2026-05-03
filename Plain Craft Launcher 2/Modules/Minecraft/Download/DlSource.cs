using PCL.Core.App;
using PCL.Core.Minecraft.Download;

namespace PCL;

public class DlSource
{
    /// DlSource | 镜像下载源

    public static bool DlPreferMojang;

    /// <summary>
    ///     下载文件（而非获取版本列表）的时候，是否优先使用官方源。
    /// </summary>
    public static bool DlSourcePreferMojang =>
        Config.Download.FileSource == 2 ||
        (Config.Download.FileSource == 1 && DlPreferMojang);

    /// <summary>
    ///     获取下载文件用的 Provider 链。
    /// </summary>
    private static DownloadProviderChain GetFileProviderChain() =>
        new(DlSourcePreferMojang);

    /// <summary>
    ///     下载文件（而非获取版本列表）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    /// </summary>
    public static IEnumerable<string> DlSourceOrder(IEnumerable<string> OfficialUrls, IEnumerable<string> MirrorUrls)
    {
        return DlSourcePreferMojang ? OfficialUrls.Union(MirrorUrls) : MirrorUrls.Union(OfficialUrls);
    }

    /// <summary>
    ///     获取版本列表（而非下载文件）的时候，是否优先使用官方源。
    /// </summary>
    public static bool DlVersionListPreferMojang =>
        Config.Download.VersionListSource == 2 ||
        (Config.Download.VersionListSource == 1 && DlPreferMojang);

    /// <summary>
    ///     获取版本列表（而非下载文件）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    /// </summary>
    public static IEnumerable<string> DlVersionListOrder(IEnumerable<string> OfficialUrls,
        IEnumerable<string> MirrorUrls)
    {
        return DlVersionListPreferMojang ? OfficialUrls.Union(MirrorUrls) : MirrorUrls.Union(OfficialUrls);
    }

    /// <summary>
    ///     下载 Assets 文件。
    /// </summary>
    public static IEnumerable<string> DlSourceAssetsGet(string original)
    {
        return GetFileProviderChain().GetAssetUrls(original);
    }

    /// <summary>
    ///     下载 Libraries 文件。
    /// </summary>
    public static IEnumerable<string> DlSourceLibraryGet(string original)
    {
        return GetFileProviderChain().GetLibraryUrls(original);
    }

    /// <summary>
    ///     下载 Launcher 或 Meta 文件。
    ///     不应使用它来获取版本列表（因为它只使用文件下载源设置来决定源顺序）。
    /// </summary>
    public static IEnumerable<string> DlSourceLauncherOrMetaGet(string original)
    {
        if (original is null)
            throw new Exception("无对应的 json 下载地址");
        return GetFileProviderChain().GetLauncherMetaUrls(original);
    }

    /// <summary>
    ///     Mod Api 镜像源
    /// </summary>
    public static string DlSourceModGet(string original)
    {
        return ModDownloadSourceResolver.GetModApiUrl(original);
    }

    /// <summary>
    ///     Mod 下载镜像源
    /// </summary>
    public static List<string> DlSourceModDownloadGet(string original)
    {
        return ModDownloadSourceResolver.GetModDownloadUrls(original, Config.Download.Comp.CompSourceSolution);
    }

    /// <summary>
    ///     Helper to build a source-ordered loader list for version list fetching.
    ///     Eliminates the repeated triple-switch pattern in every version list main method.
    /// </summary>
    public static List<KeyValuePair<ModLoader.LoaderTask<TIn, TOut>, int>>
        DlSourceVersionListGet<TIn, TOut>(
            ModLoader.LoaderTask<TIn, TOut> officialLoader,
            ModLoader.LoaderTask<TIn, TOut> bmclapiLoader,
            int? mirrorTimeout = null,
            int? officialTimeout = null)
    {
        var mt = mirrorTimeout ?? 30;
        var ot = officialTimeout ?? 60;

        return Config.Download.VersionListSource switch
        {
            0 => [new(bmclapiLoader, mt), new(officialLoader, mt + ot)],       // Mirror first
            1 => [new(officialLoader, 5), new(bmclapiLoader, 35)],             // Official first
            _ => [new(officialLoader, ot), new(bmclapiLoader, ot * 2)],        // Auto
        };
    }

    // Loader 自动切换
    public static void DlSourceLoader<InputType, OutputType>(ModLoader.LoaderTask<InputType, OutputType> MainLoader,
        List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList, bool IsForceRestart = false)
    {
        var WaitCycle = 0;
        while (true)
        {
            // 检查状态
            var BeforeLoadersAllFailed = true;
            foreach (var SubLoader in LoaderList)
            {
                if (WaitCycle == 0) // 判断是否可以不加载，直接使用已经加载好的结果
                {
                    if (IsForceRestart)
                        continue; // 强制刷新，不行
                    if (SubLoader.Key.Input is null ^ MainLoader.Input is null || (SubLoader.Key.Input is not null &&
                            !SubLoader.Key.Input.Equals(MainLoader.Input)))
                        continue; // 父子加载器的输入不一样，也不行
                }

                if (SubLoader.Key.State != ModBase.LoadState.Failed)
                    BeforeLoadersAllFailed = false;
                if (SubLoader.Key.State == ModBase.LoadState.Finished)
                {
                    MainLoader.Output = SubLoader.Key.Output;
                    DlSourceLoaderAbort(LoaderList);
                    return;
                }

                if (BeforeLoadersAllFailed)
                    if (WaitCycle < SubLoader.Value * 100)
                        WaitCycle = SubLoader.Value * 100;
            }

            // 第一轮时：既然不直接使用已经加载好的结果，那就启动第一个加载器
            if (WaitCycle == 0)
            {
                LoaderList.First().Key.Start(MainLoader.Input, IsForceRestart);
                foreach (var Loader in LoaderList.Skip(1))
                    Loader.Key.State = ModBase.LoadState.Waiting;
            }

            // 检查加载器失败或超时
            for (int i = 0, loopTo = LoaderList.Count - 1; i <= loopTo; i++)
            {
                if (WaitCycle != LoaderList[i].Value * 100)
                    continue;
                if (i < LoaderList.Count - 1 && !LoaderList.All(l => l.Key.State == ModBase.LoadState.Failed))
                {
                    LoaderList[i + 1].Key.Start(MainLoader.Input, IsForceRestart);
                }
                else
                {
                    Exception ErrorInfo = null;
                    for (int ii = 0, loopTo1 = LoaderList.Count - 1; ii <= loopTo1; ii++)
                    {
                        LoaderList[ii].Key.Input = default;
                        if (LoaderList[ii].Key.Error is not null)
                            if (ErrorInfo is null || LoaderList[ii].Key.Error.Message.Contains("无可用版本"))
                                ErrorInfo = LoaderList[ii].Key.Error;
                    }

                    if (ErrorInfo is null)
                        ErrorInfo = new TimeoutException("下载源连接超时");
                    DlSourceLoaderAbort(LoaderList);
                    throw ErrorInfo;
                }

                break;
            }

            Thread.Sleep(10);
            WaitCycle += 1;
            if (MainLoader.IsAborted)
            {
                DlSourceLoaderAbort(LoaderList);
                return;
            }
        }
    }

    private static void DlSourceLoaderAbort<InputType, OutputType>(
        List<KeyValuePair<ModLoader.LoaderTask<InputType, OutputType>, int>> LoaderList)
    {
        foreach (var Loader in LoaderList)
            if (Loader.Key.State == ModBase.LoadState.Loading)
                Loader.Key.Abort();
    }
}