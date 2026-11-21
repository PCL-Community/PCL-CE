namespace PCL;

/// <summary>依赖 Comp 文件列表的下载加载器，保留在 UI 程序集以避免下载业务依赖 Comp。</summary>
public static class CompDownloadLoaders
{
    /// <summary>Fabric API 列表，官方源。</summary>
    public static readonly Loading.LoaderTask<int, List<Comp.CompFile>> DlFabricApiLoader = new(
        "Fabric API List Loader",
        task => task.output = Comp.CompFilesGet("fabric-api", false));

    /// <summary>OptiFabric 列表，官方源。</summary>
    public static readonly Loading.LoaderTask<int, List<Comp.CompFile>> DlOptiFabricLoader = new(
        "OptiFabric List Loader",
        task => task.output = Comp.CompFilesGet("322385", true));

    /// <summary>Legacy Fabric API 列表，官方源。</summary>
    public static readonly Loading.LoaderTask<int, List<Comp.CompFile>> DlLegacyFabricApiLoader =
        new(
            "Legacy Fabric API List Loader",
            task => task.output = Comp.CompFilesGet("legacy-fabric-api", false));
}