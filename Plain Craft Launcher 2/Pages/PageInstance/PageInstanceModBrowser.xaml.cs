using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceModBrowser
{
    private const int PageSize = 20;

    private static McInstance? _contextInstance;
    private static string? _contextVanillaName;
    private static ModComp.CompLoaderType _contextLoader = ModComp.CompLoaderType.Any;
    private static bool _contextUseInstanceFolder;
    private readonly ModComp.CompProjectStorage _storage = new();
    private readonly MyLoadingStateSimulator _loadSim = new();
    private ModLoader.LoaderTask<ModComp.CompProjectRequest, int>? _loader;
    private bool _isLoading;
    private bool _hasMore = true;
    private string _lastQuery = "";

    public PageInstanceModBrowser()
    {
        InitializeComponent();
        Load.State = _loadSim;
        Load.Click += (_, _) =>
        {
            if (_loadSim.LoadingState == MyLoading.MyLoadingState.Error)
                StartSearch();
        };
        PanSearchBox.Search += (_, _) => StartSearch();
        PanSearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) StartSearch();
        };
        PageEnter += StartSearch;
    }

    /// <summary>
    ///     从 McInstance 设置上下文。
    /// </summary>
    public static void SetContext(McInstance instance)
    {
        _contextInstance = instance;
        _contextVanillaName = null;
        _contextLoader = ModComp.CompLoaderType.Any;
        _contextUseInstanceFolder = false;
    }

    /// <summary>
    ///     直接指定版本和加载器（安装新实例时使用，无需等待实例 JSON 就绪）。
    /// </summary>
    public static void SetContext(string vanillaName, string? instanceNameOrPath, ModComp.CompLoaderType loader)
    {
        _contextInstance = instanceNameOrPath is not null ? new McInstance(instanceNameOrPath) : null;
        _contextVanillaName = vanillaName;
        _contextLoader = loader;
        _contextUseInstanceFolder = true;
    }

    public static InstanceModContext? GetContext()
    {
        if (_contextInstance is null)
            return null;
        return CreateContext(_contextInstance, _contextVanillaName, _contextLoader, _contextUseInstanceFolder);
    }

    public static InstanceModContext? CreateContext(
        McInstance? instance,
        string? vanillaName = null,
        ModComp.CompLoaderType loader = ModComp.CompLoaderType.Any,
        bool useInstanceFolder = false)
    {
        if (instance is null)
            return null;

        var targetVanillaName = vanillaName;
        if (string.IsNullOrWhiteSpace(targetVanillaName))
            try
            {
                targetVanillaName = instance.Info.VanillaName;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[ModBrowser] 无法从实例读取 Minecraft 版本");
            }

        if (string.IsNullOrWhiteSpace(targetVanillaName))
            return null;

        var loaders = loader == ModComp.CompLoaderType.Any
            ? GetLoaderTypes(instance)
            : [loader];
        var modsFolder = GetModsFolder(instance, useInstanceFolder);
        return new InstanceModContext(instance, targetVanillaName, loaders, modsFolder);
    }

    private void StartSearch()
    {
        if (_contextInstance is null || _isLoading) return;

        var context = GetContext();
        if (context is not null)
            try { Directory.CreateDirectory(context.ModsFolder); } catch { }

        _isLoading = true;
        _hasMore = true;
        _lastQuery = PanSearchBox.Text?.Trim() ?? "";

        CardResults.Visibility = Visibility.Collapsed;
        PanLoad.Visibility = Visibility.Visible;
        PanLoadMore.Visibility = Visibility.Collapsed;
        HintError.Visibility = Visibility.Collapsed;
        PanResults.Children.Clear();
        _storage.results.Clear();
        _storage.curseForgeOffset = 0;
        _storage.modrinthOffset = 0;
        _storage.curseForgeTotal = -1;
        _storage.modrinthTotal = -1;

        Load.Text = Lang.Text("Instance.ModBrowser.Loading.Projects");
        Load.TextError = "";
        _loadSim.LoadingState = MyLoading.MyLoadingState.Run;

        DoLoad(0);
    }

    private void LoadNextPage()
    {
        if (_isLoading || !_hasMore) return;
        _isLoading = true;
        PanLoad.Visibility = Visibility.Collapsed;
        PanLoadMore.Visibility = Visibility.Visible;
        DoLoad(_storage.results.Count / PageSize);
    }

    private void DoLoad(int page)
    {
        var context = GetContext();
        var vanillaName = context?.VanillaName;
        var loaderType = context?.PrimaryLoader ?? ModComp.CompLoaderType.Any;

        if (string.IsNullOrEmpty(vanillaName))
        {
            ModBase.RunInUi(() =>
            {
                PanLoad.Visibility = Visibility.Collapsed;
                Load.TextError = Lang.Text("Instance.ModBrowser.Error.MissingVersion");
                _loadSim.LoadingState = MyLoading.MyLoadingState.Error;
                _isLoading = false;
            });
            return;
        }

        _loader = new ModLoader.LoaderTask<ModComp.CompProjectRequest, int>(
            Lang.Text("Instance.ModBrowser.Task.Search"),
            ModComp.CompProjectsGet,
            () => new ModComp.CompProjectRequest(ModComp.CompType.Mod, _storage, (page + 1) * PageSize)
            {
                gameVersion = vanillaName,
                modLoader = loaderType,
                searchText = _lastQuery,
                sort = ModComp.CompSortType.Downloads,
                source = ModComp.CompSourceType.Any
            })
        { reloadTimeout = 60 * 1000 };

        _loader.OnStateChanged = _ =>
        {
            if (_loader.State == ModBase.LoadState.Finished)
            {
                var libKey = Lang.Text("Download.Comp.Category.Library");
                var currentContext = GetContext();
                var downloadedIds = GetDownloadedModIds(currentContext?.ModsFolder);
                var shownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in PanResults.Children)
                    if (child is MyCompItem mc && mc.Tag is ModComp.CompProject cp)
                        shownIds.Add(cp.Id);

                var newItems = _storage.results
                    .Skip(page * PageSize)
                    .Where(r => !r.Tags.Contains(libKey) &&
                                !downloadedIds.Contains(r.Id) &&
                                !MyCompItem.DownloadedProjectIds.Contains(r.Id) &&
                                !shownIds.Contains(r.Id))
                    .DistinctBy(r => r.Id)
                    .ToList();

                ModBase.RunInUi(() =>
                {
                    PanLoad.Visibility = Visibility.Collapsed;
                    PanLoadMore.Visibility = Visibility.Collapsed;
                    _isLoading = false;
                    RenderItems(newItems);

                    var curseForgeDone = _storage.curseForgeOffset >= _storage.curseForgeTotal &&
                                         _storage.curseForgeTotal >= 0;
                    var modrinthDone = _storage.modrinthOffset >= _storage.modrinthTotal &&
                                       _storage.modrinthTotal >= 0;
                    _hasMore = !curseForgeDone || !modrinthDone;

                    if (PanResults.Children.Count == 0)
                    {
                        _hasMore = false;
                        PanLoad.Visibility = Visibility.Visible;
                        PanLoadMore.Visibility = Visibility.Collapsed;
                        Load.TextError = Lang.Text("Instance.ModBrowser.Error.NoResults");
                        _loadSim.LoadingState = MyLoading.MyLoadingState.Error;
                    }
                });
            }
            else if (_loader.State == ModBase.LoadState.Failed)
            {
                ModBase.RunInUi(() =>
                {
                    _isLoading = false;
                    _hasMore = false;
                    PanLoad.Visibility = Visibility.Visible;
                    PanLoadMore.Visibility = Visibility.Collapsed;
                    Load.TextError = Lang.Text("Instance.ModBrowser.Error.SearchFailed",
                        _loader.Error?.Message ?? Lang.Text("Instance.ModBrowser.Error.CheckNetwork"));
                    _loadSim.LoadingState = MyLoading.MyLoadingState.Error;
                });
            }
        };

        _loader.Start();
    }

    private void RenderItems(List<ModComp.CompProject> items)
    {
        if (items.Count == 0 && PanResults.Children.Count == 0) return;
        CardResults.Visibility = Visibility.Visible;
        CardResults.Opacity = 0;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaOpacity(CardResults, 1, 200, 0)
        }, "ModBrowserShowResults", true);

        foreach (var result in items)
        {
            var virtualItem = result.ToCompItem(false, false);
            var compItem = (MyCompItem)virtualItem;
            compItem.SkipDefaultNavigation = true;
            compItem.ShowInstanceButtons = true;
            compItem.Click += (_, _) =>
            {
                var context = GetContext();
                if (context is null) return;
                PageInstanceModDetail.SetContext(result, context);
                ModMain.frmMain.PageChange(FormMain.PageType.InstanceModDetail);
            };
            PanResults.Children.Add(compItem);
        }
    }

    private void PanBack_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isLoading || !_hasMore) return;
        var sv = (MyScrollViewer)sender;
        if (sv.VerticalOffset + sv.ViewportHeight + 200 >= sv.ExtentHeight)
            LoadNextPage();
    }

    private static List<ModComp.CompLoaderType> GetLoaderTypes(McInstance? instance)
    {
        if (instance is null) return [];
        try
        {
            var loaders = new List<ModComp.CompLoaderType>();
            if (instance.Info.HasFabric) loaders.Add(ModComp.CompLoaderType.Fabric);
            if (instance.Info.HasForge) loaders.Add(ModComp.CompLoaderType.Forge);
            if (instance.Info.HasNeoForge) loaders.Add(ModComp.CompLoaderType.NeoForge);
            if (instance.Info.HasQuilt) loaders.Add(ModComp.CompLoaderType.Quilt);
            return loaders;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[ModBrowser] 无法从实例读取加载器信息");
        }

        return [];
    }

    private static string GetModsFolder(McInstance instance, bool useInstanceFolder)
    {
        if (useInstanceFolder)
            return Path.Combine(instance.PathInstance, "mods");

        try
        {
            return Path.Combine(instance.PathIndie, "mods");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[ModBrowser] 无法读取实例隔离目录，改用实例目录");
            return Path.Combine(instance.PathInstance, "mods");
        }
    }

    /// <summary>
    ///     扫描 mods 文件夹，提取已安装模组的项目 ID 集合（SourceProjectId）。
    /// </summary>
    private static HashSet<string> GetDownloadedModIds(string? modsFolder)
    {
        if (string.IsNullOrEmpty(modsFolder) || !System.IO.Directory.Exists(modsFolder))
            return [];

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installed = ModCompDependency.ScanInstalledMods(modsFolder);
        foreach (var m in installed)
        {
            if (!string.IsNullOrEmpty(m.SourceProjectId))
                ids.Add(m.SourceProjectId);
        }
        return ids;
    }
}

public sealed class InstanceModContext
{
    public InstanceModContext(
        McInstance instance,
        string vanillaName,
        List<ModComp.CompLoaderType> loaders,
        string modsFolder)
    {
        Instance = instance;
        VanillaName = vanillaName;
        Loaders = loaders;
        ModsFolder = modsFolder;
    }

    public McInstance Instance { get; }
    public string VanillaName { get; }
    public List<ModComp.CompLoaderType> Loaders { get; }
    public string ModsFolder { get; }
    public ModComp.CompLoaderType PrimaryLoader => Loaders.Count > 0 ? Loaders[0] : ModComp.CompLoaderType.Any;
}
